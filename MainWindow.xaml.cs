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
        // --- VARIÁVEIS ---
        private string caminhoSelecionado = "";
        private bool modoPacote = false;
        private string codigoIdiomaDestino = "pt";
        private bool interromper = false;

        // --- CONFIGURAÇÕES DE VELOCIDADE (Para evitar BAN de IP) ---
        private const int ESPERA_ENTRE_MODS = 1500;
        private const int ESPERA_ENTRE_PACOTES = 300;
        private const int TAMANHO_LOTE = 30;

        public MainWindow()
        {
            InitializeComponent();
        }

        // --- CONTROLES DA JANELA ---
        private void TopBar_MouseDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) this.DragMove(); }
        private void BtnClose_Click(object sender, RoutedEventArgs e) { interromper = true; Application.Current.Shutdown(); }
        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        // --- SELEÇÃO DE MODO (ÚNICO OU PACOTE) ---
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

        // --- SELECIONAR ARQUIVO OU PASTA ---
        private void BtnSec_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Mod de Minecraft|*.jar";
            ofd.Title = modoPacote ? "Selecione QUALQUER MOD dentro da sua pasta 'mods'" : "Selecione o arquivo .jar do Mod";

            if (ofd.ShowDialog() == true)
            {
                if (modoPacote)
                {
                    caminhoSelecionado = System.IO.Path.GetDirectoryName(ofd.FileName);
                    int quantidade = Directory.GetFiles(caminhoSelecionado, "*.jar").Length;
                    txtYol.Text = caminhoSelecionado;
                    txtInfo.Text = $"{quantidade} Mods Encontrados";
                    AdicionarLog($"Pasta: {caminhoSelecionado} ({quantidade} arquivos)");
                }
                else
                {
                    caminhoSelecionado = ofd.FileName;
                    txtYol.Text = System.IO.Path.GetFileName(caminhoSelecionado);
                    txtInfo.Text = "Mod Único";
                    AdicionarLog($"Arquivo: {txtYol.Text}");
                }
                txtYol.Foreground = (Brush)FindResource("AccentColor");
                txtYol.FontStyle = FontStyles.Normal;
                btnBaslat.IsEnabled = true;

                progTotal.Value = 0; progCurrent.Value = 0;
                txtTotalYuzde.Text = "0 / 0"; txtCurrentYuzde.Text = "%0";
            }
        }

        private void AdicionarLog(string mensagem)
        {
            txtLog.Text += $"\n[{DateTime.Now:HH:mm:ss}] {mensagem}";
            scrlLog.ScrollToBottom();
        }

        // --- BOTÃO INICIAR ---
        private async void BtnBaslat_Click(object sender, RoutedEventArgs e)
        {
            var itemSelecionado = (ComboBoxItem)cmbLanguage.SelectedItem;
            codigoIdiomaDestino = itemSelecionado.Tag.ToString();

            btnBaslat.IsEnabled = false; btnSec.IsEnabled = false;
            cmbMode.IsEnabled = false; cmbLanguage.IsEnabled = false;
            interromper = false;

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                try
                {
                    if (modoPacote) await ProcessarPacoteMods(client);
                    else await ProcessarModUnico(client, caminhoSelecionado);

                    AdicionarLog("🏁 PROCESSAMENTO CONCLUÍDO COM SUCESSO.");
                    MessageBox.Show("A tradução foi concluída!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex) { AdicionarLog($"ERRO CRÍTICO: {ex.Message}"); }
            }

            btnBaslat.IsEnabled = true; btnSec.IsEnabled = true;
            cmbMode.IsEnabled = true; cmbLanguage.IsEnabled = true;
        }

        private async Task ProcessarModUnico(HttpClient client, string caminhoArquivo)
        {
            txtCurrentStatus.Text = $"Traduzindo: {System.IO.Path.GetFileName(caminhoArquivo)}";
            bool resultado = await TraduzirMod(caminhoArquivo, client);
            if (resultado) AdicionarLog("✅ Concluído.");
            else AdicionarLog("⚠️ Sem arquivos de idioma (en_us) para traduzir.");
            progCurrent.Value = 100; txtCurrentYuzde.Text = "%100";
        }

        private async Task ProcessarPacoteMods(HttpClient client)
        {
            string[] arquivos = Directory.GetFiles(caminhoSelecionado, "*.jar");
            int total = arquivos.Length;
            progTotal.Maximum = total;

            AdicionarLog($"=== INICIANDO TRADUÇÃO DE {total} MODS ===");

            for (int i = 0; i < total; i++)
            {
                if (interromper) { AdicionarLog("⛔ Operação cancelada pelo usuário."); break; }

                string arquivo = arquivos[i];
                string nome = System.IO.Path.GetFileName(arquivo);

                txtTotalYuzde.Text = $"{i + 1} / {total}";
                progTotal.Value = i + 1;
                txtCurrentStatus.Text = $"[{i + 1}/{total}] {nome}";
                AdicionarLog($"Processando: {nome}...");

                try
                {
                    bool resultado = await TraduzirMod(arquivo, client);
                    if (!resultado) AdicionarLog($"⏩ Ignorado (Idiomas não encontrados).");
                }
                catch (Exception ex) { AdicionarLog($"❌ Erro no mod {nome}: {ex.Message}"); }

                await Task.Delay(ESPERA_ENTRE_MODS);
            }
        }

        // --- MOTOR DE TRADUÇÃO ---
        private async Task<bool> TraduzirMod(string caminhoJar, HttpClient client)
        {
            byte[] bytesArquivo;
            try { bytesArquivo = await File.ReadAllBytesAsync(caminhoJar); } catch { return false; }

            using (MemoryStream ms = new MemoryStream())
            {
                await ms.WriteAsync(bytesArquivo, 0, bytesArquivo.Length);
                ms.Position = 0;

                using (ZipArchive archive = new ZipArchive(ms, ZipArchiveMode.Update, true))
                {
                    ZipArchiveEntry arquivoIdioma = null;
                    string caminhoAssets = "";
                    bool ehJson = true;

                    // 1. Localizar arquivo de origem (en_us)
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.FullName.EndsWith("en_us.json", StringComparison.OrdinalIgnoreCase))
                        {
                            arquivoIdioma = entry; 
                            caminhoAssets = System.IO.Path.GetDirectoryName(entry.FullName).Replace("\\", "/");
                            ehJson = true; break;
                        }
                    }
                    if (arquivoIdioma == null)
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (entry.FullName.EndsWith("en_us.lang", StringComparison.OrdinalIgnoreCase) ||
                                entry.FullName.EndsWith("en_US.lang", StringComparison.OrdinalIgnoreCase))
                            {
                                arquivoIdioma = entry; 
                                caminhoAssets = System.IO.Path.GetDirectoryName(entry.FullName).Replace("\\", "/");
                                ehJson = false; break;
                            }
                        }
                    }

                    if (arquivoIdioma == null) return false;

                    // 2. Ler conteúdo original
                    Dictionary<string, string> dicionarioOriginal = new Dictionary<string, string>();
                    using (StreamReader reader = new StreamReader(arquivoIdioma.Open()))
                    {
                        string conteudo = await reader.ReadToEndAsync();
                        if (ehJson)
                        {
                            var options = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip };
                            try { dicionarioOriginal = JsonSerializer.Deserialize<Dictionary<string, string>>(conteudo, options); } catch { return false; }
                        }
                        else { dicionarioOriginal = TraduzirArquivoLang(conteudo); }
                    }

                    if (dicionarioOriginal == null || dicionarioOriginal.Count == 0) return false;

                    // 3. Filtrar textos válidos
                    List<string> chavesParaTraduzir = new List<string>();
                    List<string> valoresParaTraduzir = new List<string>();
                    Dictionary<string, string> novoDicionario = new Dictionary<string, string>();

                    foreach (var item in dicionarioOriginal)
                    {
                        // Ignora textos vazios ou variáveis complexas
                        if (string.IsNullOrWhiteSpace(item.Value) || item.Value.Length < 2 || (item.Value.Contains("{") && item.Value.Contains("}")))
                            novoDicionario[item.Key] = item.Value;
                        else
                        {
                            chavesParaTraduzir.Add(item.Key); 
                            valoresParaTraduzir.Add(item.Value);
                        }
                    }

                    // 4. Tradução em Lote (Batch)
                    int totalStrings = chavesParaTraduzir.Count;
                    if (totalStrings > 0)
                    {
                        List<string> loteChaves = new List<string>();
                        List<string> loteValores = new List<string>();
                        int tamanhoAtual = 0;

                        for (int i = 0; i < totalStrings; i++)
                        {
                            string k = chavesParaTraduzir[i]; 
                            string v = valoresParaTraduzir[i];
                            int len = v.Length + 5;

                            if (loteValores.Count > 0 && (tamanhoAtual + len > 1600 || loteValores.Count >= TAMANHO_LOTE))
                            {
                                await ProcessarLoteTraducao(client, loteChaves, loteValores, novoDicionario);

                                if (!modoPacote)
                                {
                                    Application.Current.Dispatcher.Invoke(() => {
                                        progCurrent.Value = ((double)i / totalStrings) * 100;
                                        txtCurrentYuzde.Text = $"%{progCurrent.Value:0}";
                                    });
                                }
                                loteChaves.Clear(); loteValores.Clear(); tamanhoAtual = 0;
                                await Task.Delay(ESPERA_ENTRE_PACOTES);
                            }
                            loteChaves.Add(k); loteValores.Add(v); tamanhoAtual += len;
                        }
                        if (loteValores.Count > 0) await ProcessarLoteTraducao(client, loteChaves, loteValores, novoDicionario);
                    }

                    // 5. Salvar tradução no arquivo
                    string nomeArquivoDestino = DefinirNomeArquivoDestino(codigoIdiomaDestino, ehJson);
                    string caminhoFinal = caminhoAssets + "/" + nomeArquivoDestino;
                    string conteudoFinal = "";

                    if (ehJson)
                    {
                        var writeOptions = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                        conteudoFinal = JsonSerializer.Serialize(novoDicionario, writeOptions);
                    }
                    else
                    {
                        StringBuilder sb = new StringBuilder();
                        foreach (var kvp in novoDicionario) sb.AppendLine($"{kvp.Key}={kvp.Value}");
                        conteudoFinal = sb.ToString();
                    }

                    var entradaAntiga = archive.GetEntry(caminhoFinal);
                    if (entradaAntiga != null) entradaAntiga.Delete();

                    var novaEntrada = archive.CreateEntry(caminhoFinal);
                    using (StreamWriter writer = new StreamWriter(novaEntrada.Open())) { await writer.WriteAsync(conteudoFinal); }
                }

                await File.WriteAllBytesAsync(caminhoJar, ms.ToArray());
            }

            return true;
        }

        // --- UTILITÁRIOS E API ---
        private Dictionary<string, string> TraduzirArquivoLang(string conteudo)
        {
            var dicionario = new Dictionary<string, string>();
            using (StringReader sr = new StringReader(conteudo))
            {
                string linha;
                while ((linha = sr.ReadLine()) != null)
                {
                    if (linha.Trim().StartsWith("#") || string.IsNullOrWhiteSpace(linha)) continue;
                    int indice = linha.IndexOf('=');
                    if (indice > 0)
                    {
                        string chave = linha.Substring(0, indice).Trim();
                        string valor = linha.Substring(indice + 1).Trim();
                        if (!dicionario.ContainsKey(chave)) dicionario.Add(chave, valor);
                    }
                }
            }
            return dicionario;
        }

        private async Task ProcessarLoteTraducao(HttpClient client, List<string> chaves, List<string> valores, Dictionary<string, string> dicionario)
        {
            List<string> resultados = await ChamarApiTraducao(client, valores);
            if (resultados.Count != valores.Count)
            {
                for (int j = 0; j < chaves.Count; j++)
                {
                    dicionario[chaves[j]] = await TraduzirTextoUnico(client, valores[j]);
                    await Task.Delay(150);
                }
            }
            else
            {
                for (int j = 0; j < chaves.Count; j++) dicionario[chaves[j]] = resultados[j];
            }
        }

        private async Task<List<string>> ChamarApiTraducao(HttpClient client, List<string> textos)
        {
            try
            {
                string combinado = string.Join("\n", textos);
                string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={codigoIdiomaDestino}&dt=t&q={Uri.EscapeDataString(combinado)}";
                string resposta = await client.GetStringAsync(url);
                List<string> lista = new List<string>();
                using (JsonDocument doc = JsonDocument.Parse(resposta))
                {
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var s in root[0].EnumerateArray())
                        {
                            if (s.ValueKind == JsonValueKind.Array)
                            {
                                string t = s[0].GetString();
                                if (!string.IsNullOrEmpty(t)) lista.Add(LimparTexto(t));
                            }
                        }
                    }
                }
                return lista;
            }
            catch { return new List<string>(); }
        }

        private async Task<string> TraduzirTextoUnico(HttpClient client, string texto)
        {
            try
            {
                string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={codigoIdiomaDestino}&dt=t&q={Uri.EscapeDataString(texto)}";
                string resposta = await client.GetStringAsync(url);
                using (JsonDocument doc = JsonDocument.Parse(resposta))
                    return LimparTexto(doc.RootElement[0][0][0].GetString());
            }
            catch { return texto; }
        }

        private string LimparTexto(string t)
        {
            // Protege variáveis do Minecraft contra erros de tradução
            return t.Trim()
                .Replace("% s", " %s")
                .Replace("% d", " %d")
                .Replace("§ ", "§")
                .Replace("& ", "§");
        }

        private string DefinirNomeArquivoDestino(string codigo, bool ehJson)
        {
            string extensao = ehJson ? "json" : "lang";
            if (!ehJson && codigo == "pt") return "pt_BR.lang";

            switch (codigo)
            {
                case "pt": return $"pt_br.{extensao}";
                case "en": return $"en_us.{extensao}";
                case "tr": return $"tr_tr.{extensao}";
                default: return $"{codigo}_{codigo}.{extensao}";
            }
        }
    }
}
