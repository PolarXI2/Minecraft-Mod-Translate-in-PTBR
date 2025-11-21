using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Linq;
using System.Text;

namespace ModernModTranslator
{
    public partial class MainWindow : Window
    {
        // --- DEĞİŞKENLER ---
        private string secilenYol = "";
        private bool isBatchMode = false;
        private string hedefDilKodu = "tr";
        private bool iptal = false;

        // --- AYARLAR ---
        private const int MOD_ARASI_BEKLEME = 1500;
        private const int PAKET_ARASI_BEKLEME = 300;
        private const int BATCH_SIZE = 30;

        public MainWindow()
        {
            InitializeComponent();
        }

        // --- PENCERE ---
        private void TopBar_MouseDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) this.DragMove(); }
        private void BtnClose_Click(object sender, RoutedEventArgs e) { iptal = true; Application.Current.Shutdown(); }
        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        // --- MOD SEÇİMİ ---
        private void CmbMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (btnSec == null || pnlTotalProgress == null || txtInfo == null) return;

            isBatchMode = cmbMode.SelectedIndex == 1;

            if (isBatchMode)
            {
                btnSec.Content = "MODS KLASÖRÜ SEÇ";
                pnlTotalProgress.Visibility = Visibility.Visible;
                txtInfo.Text = "";
            }
            else
            {
                btnSec.Content = "MOD DOSYASI SEÇ";
                pnlTotalProgress.Visibility = Visibility.Collapsed;
                txtInfo.Text = "";
            }
            secilenYol = "";
            if (txtYol != null) txtYol.Text = "Seçim bekleniyor...";
            if (btnBaslat != null) btnBaslat.IsEnabled = false;
        }

        // --- DOSYA/KLASÖR SEÇ ---
        private void BtnSec_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Minecraft Modu|*.jar";
            ofd.Title = isBatchMode ? "Klasör algılamak için İÇERİDEN BİR MOD seçin" : "Mod Dosyasını Seçin";

            if (ofd.ShowDialog() == true)
            {
                if (isBatchMode)
                {
                    secilenYol = System.IO.Path.GetDirectoryName(ofd.FileName);
                    int sayi = Directory.GetFiles(secilenYol, "*.jar").Length;
                    txtYol.Text = secilenYol;
                    txtInfo.Text = $"{sayi} Mod Bulundu";
                    LogEkle($"Klasör: {secilenYol} ({sayi} dosya)");
                }
                else
                {
                    secilenYol = ofd.FileName;
                    txtYol.Text = System.IO.Path.GetFileName(secilenYol);
                    txtInfo.Text = "Tek Dosya";
                    LogEkle($"Dosya: {txtYol.Text}");
                }
                txtYol.Foreground = (Brush)FindResource("AccentColor");
                txtYol.FontStyle = FontStyles.Normal;
                btnBaslat.IsEnabled = true;

                progTotal.Value = 0; progCurrent.Value = 0;
                txtTotalYuzde.Text = "0 / 0"; txtCurrentYuzde.Text = "%0";
            }
        }

        private void LogEkle(string mesaj)
        {
            txtLog.Text += $"\n[{DateTime.Now:HH:mm:ss}] {mesaj}";
            scrlLog.ScrollToBottom();
        }

        // --- BAŞLAT ---
        private async void BtnBaslat_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = (ComboBoxItem)cmbLanguage.SelectedItem;
            hedefDilKodu = selectedItem.Tag.ToString();

            btnBaslat.IsEnabled = false; btnSec.IsEnabled = false;
            cmbMode.IsEnabled = false; cmbLanguage.IsEnabled = false;
            iptal = false;

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                try
                {
                    if (isBatchMode) await ModPaketiIsle(client);
                    else await TekModIsle(client, secilenYol);

                    LogEkle("🏁 İŞLEMLER TAMAMLANDI.");
                    MessageBox.Show("İşlem Başarılı!", "Bitti", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex) { LogEkle($"HATA: {ex.Message}"); }
            }

            btnBaslat.IsEnabled = true; btnSec.IsEnabled = true;
            cmbMode.IsEnabled = true; cmbLanguage.IsEnabled = true;
        }

        private async Task TekModIsle(HttpClient client, string dosyaYolu)
        {
            txtCurrentStatus.Text = $"İşleniyor: {System.IO.Path.GetFileName(dosyaYolu)}";
            bool sonuc = await ModCevir(dosyaYolu, client);
            if (sonuc) LogEkle("✅ Tamamlandı.");
            else LogEkle("⚠️ Dil dosyası yok.");
            progCurrent.Value = 100; txtCurrentYuzde.Text = "%100";
        }

        private async Task ModPaketiIsle(HttpClient client)
        {
            string[] dosyalar = Directory.GetFiles(secilenYol, "*.jar");
            int toplam = dosyalar.Length;
            progTotal.Maximum = toplam;

            LogEkle($"=== {toplam} MOD İŞLENECEK ===");

            for (int i = 0; i < toplam; i++)
            {
                if (iptal) { LogEkle("⛔ İptal edildi."); break; }

                string dosya = dosyalar[i];
                string ad = System.IO.Path.GetFileName(dosya);

                txtTotalYuzde.Text = $"{i + 1} / {toplam}";
                progTotal.Value = i + 1;
                txtCurrentStatus.Text = $"[{i + 1}/{toplam}] {ad}";
                LogEkle($"Mod: {ad}...");

                try
                {
                    bool sonuc = await ModCevir(dosya, client);
                    if (!sonuc) LogEkle($"⏩ Atlandı.");
                }
                catch (Exception ex) { LogEkle($"❌ Hata: {ex.Message}"); }

                await Task.Delay(MOD_ARASI_BEKLEME);
            }
        }

        // --- ÇEVİRİ MOTORU (DÜZELTİLEN KISIM) ---
        private async Task<bool> ModCevir(string jarYolu, HttpClient client)
        {
            byte[] fileBytes;
            try { fileBytes = await File.ReadAllBytesAsync(jarYolu); } catch { return false; }

            // [DÜZELTME BURADA] 
            // Fixed-size (sabit boyutlu) stream yerine Expandable (genişleyebilir) stream kullanıyoruz.
            using (MemoryStream ms = new MemoryStream())
            {
                // Veriyi akışa yaz
                await ms.WriteAsync(fileBytes, 0, fileBytes.Length);
                ms.Position = 0; // Başa sar

                using (ZipArchive archive = new ZipArchive(ms, ZipArchiveMode.Update, true)) // 'true' leaveOpen demektir
                {
                    ZipArchiveEntry dilDosyasi = null;
                    string assetsPath = "";
                    bool isJson = true;

                    // 1. Dosya Bul
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.FullName.EndsWith("en_us.json", StringComparison.OrdinalIgnoreCase))
                        {
                            dilDosyasi = entry; assetsPath = System.IO.Path.GetDirectoryName(entry.FullName).Replace("\\", "/");
                            isJson = true; break;
                        }
                    }
                    if (dilDosyasi == null)
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (entry.FullName.EndsWith("en_us.lang", StringComparison.OrdinalIgnoreCase) ||
                                entry.FullName.EndsWith("en_US.lang", StringComparison.OrdinalIgnoreCase))
                            {
                                dilDosyasi = entry; assetsPath = System.IO.Path.GetDirectoryName(entry.FullName).Replace("\\", "/");
                                isJson = false; break;
                            }
                        }
                    }

                    if (dilDosyasi == null) return false;

                    // 2. Oku
                    Dictionary<string, string> ceviriSozlugu = new Dictionary<string, string>();
                    using (StreamReader reader = new StreamReader(dilDosyasi.Open()))
                    {
                        string content = await reader.ReadToEndAsync();
                        if (isJson)
                        {
                            var options = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip };
                            try { ceviriSozlugu = JsonSerializer.Deserialize<Dictionary<string, string>>(content, options); } catch { return false; }
                        }
                        else { ceviriSozlugu = ParseLangFile(content); }
                    }

                    if (ceviriSozlugu == null || ceviriSozlugu.Count == 0) return false;

                    // 3. Filtrele
                    List<string> anahtarlar = new List<string>();
                    List<string> degerler = new List<string>();
                    Dictionary<string, string> yeniSozluk = new Dictionary<string, string>();

                    foreach (var item in ceviriSozlugu)
                    {
                        if (string.IsNullOrWhiteSpace(item.Value) || item.Value.Length < 2 || (item.Value.Contains("{") && item.Value.Contains("}")))
                            yeniSozluk[item.Key] = item.Value;
                        else
                        {
                            anahtarlar.Add(item.Key); degerler.Add(item.Value);
                        }
                    }

                    // 4. Batch Çeviri
                    int total = anahtarlar.Count;
                    if (total > 0)
                    {
                        List<string> batchKeys = new List<string>();
                        List<string> batchValues = new List<string>();
                        int currentLen = 0;

                        for (int i = 0; i < total; i++)
                        {
                            string k = anahtarlar[i]; string v = degerler[i];
                            int len = v.Length + 5;

                            if (batchValues.Count > 0 && (currentLen + len > 1600 || batchValues.Count >= BATCH_SIZE))
                            {
                                await ProcessBatch(client, batchKeys, batchValues, yeniSozluk);

                                if (!isBatchMode)
                                {
                                    Application.Current.Dispatcher.Invoke(() => {
                                        progCurrent.Value = ((double)i / total) * 100;
                                        txtCurrentYuzde.Text = $"%{progCurrent.Value:0}";
                                    });
                                }
                                batchKeys.Clear(); batchValues.Clear(); currentLen = 0;
                                await Task.Delay(PAKET_ARASI_BEKLEME);
                            }
                            batchKeys.Add(k); batchValues.Add(v); currentLen += len;
                        }
                        if (batchValues.Count > 0) await ProcessBatch(client, batchKeys, batchValues, yeniSozluk);
                    }

                    // 5. Kaydet
                    string hedefDosyaAdi = HedefDosyaAdiBelirle(hedefDilKodu, isJson);
                    string yeniYol = assetsPath + "/" + hedefDosyaAdi;
                    string yazilacakIcerik = "";

                    if (isJson)
                    {
                        var writeOptions = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                        yazilacakIcerik = JsonSerializer.Serialize(yeniSozluk, writeOptions);
                    }
                    else
                    {
                        StringBuilder sb = new StringBuilder();
                        foreach (var kvp in yeniSozluk) sb.AppendLine($"{kvp.Key}={kvp.Value}");
                        yazilacakIcerik = sb.ToString();
                    }

                    var eski = archive.GetEntry(yeniYol);
                    if (eski != null) eski.Delete();

                    var yeni = archive.CreateEntry(yeniYol);
                    using (StreamWriter writer = new StreamWriter(yeni.Open())) { await writer.WriteAsync(yazilacakIcerik); }
                }

                // Zip Arşivi kapandı, şimdi genişlemiş memory stream'i diske yazalım
                await File.WriteAllBytesAsync(jarYolu, ms.ToArray());
            }

            return true;
        }

        // --- PARSERS & API ---
        private Dictionary<string, string> ParseLangFile(string content)
        {
            var dict = new Dictionary<string, string>();
            using (StringReader sr = new StringReader(content))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Trim().StartsWith("#") || string.IsNullOrWhiteSpace(line)) continue;
                    int index = line.IndexOf('=');
                    if (index > 0)
                    {
                        string key = line.Substring(0, index).Trim();
                        string val = line.Substring(index + 1).Trim();
                        if (!dict.ContainsKey(key)) dict.Add(key, val);
                    }
                }
            }
            return dict;
        }

        private async Task ProcessBatch(HttpClient client, List<string> keys, List<string> values, Dictionary<string, string> dict)
        {
            List<string> results = await TranslateApi(client, values);
            if (results.Count != values.Count)
            {
                for (int j = 0; j < keys.Count; j++)
                {
                    dict[keys[j]] = await TranslateSingle(client, values[j]);
                    await Task.Delay(150);
                }
            }
            else
            {
                for (int j = 0; j < keys.Count; j++) dict[keys[j]] = results[j];
            }
        }

        private async Task<List<string>> TranslateApi(HttpClient client, List<string> texts)
        {
            try
            {
                string combined = string.Join("\n", texts);
                string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={hedefDilKodu}&dt=t&q={Uri.EscapeDataString(combined)}";
                string res = await client.GetStringAsync(url);
                List<string> list = new List<string>();
                using (JsonDocument doc = JsonDocument.Parse(res))
                {
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var s in root[0].EnumerateArray())
                        {
                            if (s.ValueKind == JsonValueKind.Array)
                            {
                                string t = s[0].GetString();
                                if (!string.IsNullOrEmpty(t)) list.Add(CleanText(t));
                            }
                        }
                    }
                }
                return list;
            }
            catch { return new List<string>(); }
        }

        private async Task<string> TranslateSingle(HttpClient client, string text)
        {
            try
            {
                string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={hedefDilKodu}&dt=t&q={Uri.EscapeDataString(text)}";
                string res = await client.GetStringAsync(url);
                using (JsonDocument doc = JsonDocument.Parse(res))
                    return CleanText(doc.RootElement[0][0][0].GetString());
            }
            catch { return text; }
        }

        private string CleanText(string t)
        {
            return t.Trim().Replace("% s", " %s").Replace("% d", " %d").Replace("§ ", "§");
        }

        private string HedefDosyaAdiBelirle(string kod, bool isJson)
        {
            string ext = isJson ? "json" : "lang";
            if (!isJson && kod == "tr") return "tr_TR.lang";

            switch (kod)
            {
                case "tr": return $"tr_tr.{ext}";
                case "en": return $"en_us.{ext}";
                case "de": return $"de_de.{ext}";
                case "fr": return $"fr_fr.{ext}";
                default: return $"{kod}_{kod}.{ext}";
            }
        }
    }
}