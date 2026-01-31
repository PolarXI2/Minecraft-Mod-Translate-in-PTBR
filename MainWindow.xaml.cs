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
using System.Text.RegularExpressions;

namespace ModernModTranslator
{
    public partial class MainWindow : Window
    {
        // --- VARIÁVEIS DE ESTADO ---
        private string caminhoSelecionado = "";
        private bool modoPacote = false;
        private string codigoIdiomaDestino = "pt";
        private bool interromper = false;

        // --- CONFIGURAÇÕES DA IA E PERFORMANCE ---
        private const string GEMINI_API_KEY = "SUA_CHAVE_AQUI_DENTRO"; 
        private const int ESPERA_ENTRE_MODS = 1500;
        private const int ESPERA_ENTRE_PACOTES = 500;
        private const int TAMANHO_LOTE = 25; 

        public MainWindow()
        {
            InitializeComponent();
        }

        // --- CONTROLES DA INTERFACE ---
        private void TopBar_MouseDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) this.DragMove(); }
        private void BtnClose_Click(object sender, RoutedEventArgs e) { interromper = true; Application.Current.Shutdown(); }
        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        private void CmbMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (btnSec == null || pnlTotalProgress == null || txtInfo == null) return;
            modoPacote = cmbMode.SelectedIndex == 1;

            if (modoPacote)
            {
                btnSec.Content = "SELECIONAR PASTA MODS";
                pnlTotalProgress.Visibility = Visibility.Visible;
                txtInfo.Text = "";
            }
            else
            {
                btnSec.Content = "SELECIONAR ARQUIVO MOD";
                pnlTotalProgress.Visibility = Visibility.Collapsed;
                txtInfo.Text = "";
            }
            caminhoSelecionado = "";
            if (txtYol != null) txtYol.Text = "Aguardando seleção...";
            if (btnBaslat != null) btnBaslat.IsEnabled = false;
        }

        private void BtnSec_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Mod de Minecraft|*.jar";
            ofd.Title = modoPacote ? "Selecione qualquer mod dentro da pasta 'mods'" : "Selecione o arquivo do Mod";

            if (ofd.ShowDialog() == true)
            {
                if (modoPacote)
                {
                    caminhoSelecionado = Path.GetDirectoryName(ofd.FileName);
                    int total = Directory.GetFiles(caminhoSelecionado, "*.jar").Length;
                    txtYol.Text = caminhoSelecionado;
                    txtInfo.Text = $"{total} Mods Detectados";
                    AdicionarLog($"Pasta: {caminhoSelecionado} ({total} arquivos)");
                }
                else
                {
                    caminhoSelecionado = ofd.FileName;
                    txtYol.Text = Path.GetFileName(caminhoSelecionado);
                    txtInfo.Text = "Mod Individual";
                    AdicionarLog($"Arquivo: {txtYol.Text}");
                }
                btnBaslat.IsEnabled = true;
                progTotal.Value = 0; progCurrent.Value = 0;
            }
        }

        private void AdicionarLog(string mensagem)
        {
            txtLog.Text += $"\n[{DateTime.Now:HH:mm:ss}] {mensagem}";
            scrlLog.ScrollToBottom();
        }

        // --- INICIAR TRADUÇÃO ---
        private async void BtnBaslat_Click(object sender, RoutedEventArgs e)
        {
            var item = (ComboBoxItem)cmbLanguage.SelectedItem;
            codigoIdiomaDestino = item.Tag.ToString();

            btnBaslat.IsEnabled = btnSec.IsEnabled = cmbMode.IsEnabled = cmbLanguage.IsEnabled = false;
            interromper = false;

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "UniversalModTranslatorBR/1.0");

                try
                {
                    if (modoPacote) await ProcessarPasta(client);
                    else await ProcessarUnico(client, caminhoSelecionado);

                    AdicionarLog("🏁 PROCESSO FINALIZADO.");
                    MessageBox.Show("Tradução concluída com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex) { AdicionarLog($"❌ ERRO: {ex.Message}"); }
            }

            btnBaslat.IsEnabled = btnSec.IsEnabled = cmbMode.IsEnabled = cmbLanguage.IsEnabled = true;
        }

        private async Task ProcessarUnico(HttpClient client, string caminho)
        {
            txtCurrentStatus.Text = $"Processando: {Path.GetFileName(caminho)}";
            bool ok = await TraduzirMod(caminho, client);
            if (ok) AdicionarLog("✅ Concluído.");
            else AdicionarLog("⚠️ Ignorado: Sem arquivos de origem.");
            progCurrent.Value = 100;
        }

        private async Task ProcessarPasta(HttpClient client)
        {
            string[] arquivos = Directory.GetFiles(caminhoSelecionado, "*.jar");
            progTotal.Maximum = arquivos.Length;

            for (int i = 0; i < arquivos.Length; i++)
            {
                if (interromper) { AdicionarLog("⛔ Cancelado."); break; }
                
                progTotal.Value = i + 1;
                txtTotalYuzde.Text = $"{i + 1} / {arquivos.Length}";
                await ProcessarUnico(client, arquivos[i]);
                await Task.Delay(ESPERA_ENTRE_MODS);
            }
        }

        // --- LÓGICA DE MANIPULAÇÃO DO .JAR ---
        private async Task<bool> TraduzirMod(string jarPath, HttpClient client)
        {
            byte[] fileBytes;
            try { fileBytes = await File.ReadAllBytesAsync(jarPath); } catch { return false; }

            using (MemoryStream ms = new MemoryStream())
            {
                await ms.WriteAsync(fileBytes, 0, fileBytes.Length);
                ms.Position = 0;

                using (ZipArchive archive = new ZipArchive(ms, ZipArchiveMode.Update, true))
                {
                    ZipArchiveEntry entryOrigem = null;
                    string pastaAssets = "";
                    bool ehJson = true;

                    // Busca o arquivo en_us
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.FullName.EndsWith("en_us.json", StringComparison.OrdinalIgnoreCase))
                        {
                            entryOrigem = entry; ehJson = true;
                            pastaAssets = Path.GetDirectoryName(entry.FullName).Replace("\\", "/");
                            break;
                        }
                        if (entry.FullName.EndsWith("en_us.lang", StringComparison.OrdinalIgnoreCase) || entry.FullName.EndsWith("en_US.lang", StringComparison.OrdinalIgnoreCase))
                        {
                            entryOrigem = entry; ehJson = false;
                            pastaAssets = Path.GetDirectoryName(entry.FullName).Replace("\\", "/");
                            break;
                        }
                    }

                    if (entryOrigem == null) return false;

                    Dictionary<string, string> dictOriginal = new Dictionary<string, string>();
                    using (StreamReader reader = new StreamReader(entryOrigem.Open()))
                    {
                        string content = await reader.ReadToEndAsync();
                        if (ehJson)
                        {
                            var opts = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip };
                            try { dictOriginal = JsonSerializer.Deserialize<Dictionary<string, string>>(content, opts); } catch { return false; }
                        }
                        else { dictOriginal = ParseLang(content); }
                    }

                    if (dictOriginal == null || dictOriginal.Count == 0) return false;

                    // Filtra e prepara lotes
                    var chaves = dictOriginal.Keys.ToList();
                    var valores = dictOriginal.Values.ToList();
                    Dictionary<string, string> resultadoFinal = new Dictionary<string, string>();

                    for (int i = 0; i < chaves.Count; i += TAMANHO_LOTE)
                    {
                        var loteChaves = chaves.Skip(i).Take(TAMANHO_LOTE).ToList();
                        var loteValores = valores.Skip(i).Take(TAMANHO_LOTE).ToList();

                        List<string> traduzidos = await ChamarIA(client, loteValores);
                        
                        for (int j = 0; j < loteChaves.Count; j++)
                        {
                            resultadoFinal[loteChaves[j]] = (traduzidos != null && j < traduzidos.Count) ? traduzidos[j] : loteValores[j];
                        }

                        // Atualiza progresso individual
                        Application.Current.Dispatcher.Invoke(() => {
                            progCurrent.Value = ((double)(i + loteChaves.Count) / chaves.Count) * 100;
                            txtCurrentYuzde.Text = $"%{(int)progCurrent.Value}";
                        });
                        await Task.Delay(ESPERA_ENTRE_PACOTES);
                    }

                    // Salva o novo arquivo
                    string nomeDestino = ObterNomeArquivo(codigoIdiomaDestino, ehJson);
                    string caminhoDestino = pastaAssets + "/" + nomeDestino;
                    
                    var entryExistente = archive.GetEntry(caminhoDestino);
                    if (entryExistente != null) entryExistente.Delete();

                    var novaEntry = archive.CreateEntry(caminhoDestino);
                    using (StreamWriter writer = new StreamWriter(novaEntry.Open()))
                    {
                        if (ehJson) await writer.WriteAsync(JsonSerializer.Serialize(resultadoFinal, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
                        else foreach (var kvp in resultadoFinal) await writer.WriteLineAsync($"{kvp.Key}={kvp.Value}");
                    }
                }
                await File.WriteAllBytesAsync(jarPath, ms.ToArray());
            }
            return true;
        }

        // --- INTEGRAÇÃO COM GEMINI AI ---
        private async Task<List<string>> ChamarIA(HttpClient client, List<string> textos)
        {
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={GEMINI_API_KEY}";
            string prompt = $"Traduza para Português Brasileiro (PT-BR) de forma contextualizada para Minecraft. " +
                            $"NÃO traduza códigos (§) ou variáveis (%s, %d). Responda APENAS com a tradução, uma por linha:\n\n" + string.Join("\n", textos);

            var body = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };
            
            try
            {
                var resp = await client.PostAsync(url, new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));
                var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                string fullText = json.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
                return fullText.Split('\n').Select(x => LimparTexto(x)).ToList();
            }
            catch { return textos; }
        }

        private Dictionary<string, string> ParseLang(string content)
        {
            var d = new Dictionary<string, string>();
            using (var sr = new StringReader(content))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                    int idx = line.IndexOf('=');
                    if (idx > 0) d[line.Substring(0, idx).Trim()] = line.Substring(idx + 1).Trim();
                }
            }
            return d;
        }

        private string LimparTexto(string t) => t.Trim().Replace("% s", " %s").Replace("§ ", "§");

        private string ObterNomeArquivo(string cod, bool json)
        {
            string ext = json ? "json" : "lang";
            if (cod == "pt") return json ? "pt_br.json" : "pt_BR.lang";
            return $"{cod}_{cod}.{ext}";
        }
    }
}
