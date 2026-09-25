using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScoundrelTeste
{
    public partial class Form1 : Form
    {
        // Variáveis do Jogo
        private List<Carta> baralho1;
        private Random rnd;
        private int vidaJogador = 20;

        private Carta equipamentoAtual = null;

        // Arma: sem "durabilidade acumulada" (regra original)
        private int valorArma = 0;
        private int ultimoMonstroComArma = int.MaxValue;

        // Sala / turno
        private bool pocaoUsadaNoTurno = false;
        private int escolhasRestantes = 3;
        private List<Carta> cartasMesa = new List<Carta>(4);
        private bool fugiuNaRodadaAnterior = false;
        private int score = 0;

        // Input / estado global
        private bool inputTravado = false;
        private bool jogoEncerrado = false;

        // UI e Som
        private List<Carta> cartasHistorico = new List<Carta>();
        private Panel[] painelHistorico;
        private SoundPlayer playerFundo;

        public Form1()
        {
            InitializeComponent();
            InicializarBaralho();
            rnd = new Random();

            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            panelBaralho.BackgroundImage = Properties.Resources.card_back1;
            panelBaralho.BackgroundImageLayout = ImageLayout.Zoom;

            painelHistorico = new Panel[] { panelHist1, panelHist2, panelHist3, panelHist4, panelHist5 };
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            FundoJogo.BackgroundImage = Properties.Resources.MesaFundo; // nome do recurso
            FundoJogo.BackgroundImageLayout = ImageLayout.Stretch; // ou Zoom
            panelEquipamento.BorderStyle = BorderStyle.None;
            panelEquipamento.BackColor = Color.Transparent;
            panelBaralho.BorderStyle = BorderStyle.None;     // ✅ remove borda do Panel
            panelBaralho.BackColor = Color.Transparent;
            panel1.BorderStyle = BorderStyle.None;
            panel2.BorderStyle = BorderStyle.None;
            panel3.BorderStyle = BorderStyle.None;
            panel4.BorderStyle = BorderStyle.None;
            for (int i = 0; i < 4; i++) cartasMesa.Add(null);
            MostrarCartasAnimadas();

            playerFundo = new SoundPlayer(Properties.Resources.somFundo);
            playerFundo.PlayLooping();
        }

        public class Carta
        {
            public string Nome { get; set; }
            public Image Image { get; set; }
            public string Naipe { get; set; }
            public int Valor { get; set; }

            public Carta(string nome, Image imagem, string naipe, int valor)
            {
                Nome = nome;
                Image = imagem;
                Naipe = naipe;
                Valor = valor;
            }
        }

        private Image GetCardImageOrBlank(string resourceKey)
        {
            var obj = Properties.Resources.ResourceManager.GetObject(resourceKey);
            if (obj is Image img) return img;
            return Properties.Resources.card_blank;
        }

        private void InicializarBaralho()
        {
            baralho1 = new List<Carta>();

            for (int i = 1; i <= 13; i++)
            {
                int valor = (i == 1) ? 14 : i;
                baralho1.Add(new Carta($"Clubs {i}", GetCardImageOrBlank($"card_clubs_{i}"), "Clubs", valor));
            }

            for (int i = 2; i <= 10; i++)
                baralho1.Add(new Carta($"Diamonds {i}", GetCardImageOrBlank($"card_diamonds_{i}"), "Diamonds", i));

            for (int i = 2; i <= 10; i++)
                baralho1.Add(new Carta($"Hearts {i}", GetCardImageOrBlank($"card_hearts_{i}"), "Hearts", i));

            for (int i = 1; i <= 13; i++)
            {
                int valor = (i == 1) ? 14 : i;
                baralho1.Add(new Carta($"Spades {i}", GetCardImageOrBlank($"card_spades_{i}"), "Spades", valor));
            }
        }

        private void MostrarCartasAnimadas()
        {
            if (jogoEncerrado) return;

            Panel[] paineis = { panel1, panel2, panel3, panel4 };

            for (int i = 0; i < paineis.Length; i++)
            {
                paineis[i].Controls.Clear();

                PictureBox pb = new PictureBox
                {
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    Dock = DockStyle.Fill,
                    BorderStyle = BorderStyle.None, // 🔥 REMOVE A BORDA
                    BackColor = Color.Transparent,
                    Tag = null,
                    Enabled = false
                };

                Carta novaCarta = cartasMesa[i];

                if (novaCarta == null)
                {
                    if (baralho1.Count > 0)
                    {
                        int index = rnd.Next(baralho1.Count);
                        novaCarta = baralho1[index];
                        baralho1.RemoveAt(index);
                    }
                    else
                    {
                        // Baralho acabou: não compra mais nada.
                        novaCarta = null;
                    }
                }

                if (novaCarta != null)
                {
                    cartasMesa[i] = novaCarta;
                    pb.Image = novaCarta.Image ?? Properties.Resources.card_blank;
                    pb.Tag = novaCarta;
                    pb.Click += Carta_Click;
                    pb.Enabled = true;

                    if (novaCarta.Naipe == "Diamonds")
                        AplicarBrilho(pb);
                }
                else
                {
                    pb.Image = Properties.Resources.card_blank;
                    pb.Enabled = false;
                }

                paineis[i].Controls.Add(pb);

                pb.Visible = false;
                Timer fadeTimer = new Timer { Interval = 30 };
                fadeTimer.Tick += (s, e) =>
                {
                    if (jogoEncerrado)
                    {
                        fadeTimer.Stop();
                        fadeTimer.Dispose();
                        return;
                    }

                    pb.Visible = true;
                    fadeTimer.Stop();
                    fadeTimer.Dispose();
                };
                fadeTimer.Start();
            }

            escolhasRestantes = 3;
            pocaoUsadaNoTurno = false;
            inputTravado = false;

            AtualizarUI();
            AtualizarCartasRestantes();
            ChecarVitoria();
        }
        private void ChecarVitoria()
        {
            // Total de cartas restantes no jogo = deck + mesa
            bool mesaVazia = cartasMesa.All(c => c == null);
            bool deckVazio = baralho1.Count == 0;

            if (deckVazio && mesaVazia && !jogoEncerrado)
            {
                Vitoria();
            }
        }
        private void Vitoria()
        {
            if (jogoEncerrado) return;

            jogoEncerrado = true;
            inputTravado = true;
            escolhasRestantes = 0;

            panel1.Enabled = false;
            panel2.Enabled = false;
            panel3.Enabled = false;
            panel4.Enabled = false;
            btnFuga.Enabled = false;

            MessageBox.Show($"Você venceu! Score final: {score}");

            Timer timer = new Timer { Interval = 1000 };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                Application.Restart();
            };
            timer.Start();
        }
        private void AplicarBrilho(PictureBox pb)
        {
            Timer brilhoTimer = new Timer { Interval = 100 };
            int brilhoCount = 0;
            bool brilhoOn = true;

            brilhoTimer.Tick += (s, e) =>
            {
                if (jogoEncerrado)
                {
                    brilhoTimer.Stop();
                    brilhoTimer.Dispose();
                    return;
                }

                pb.BorderStyle = brilhoOn ? BorderStyle.Fixed3D : BorderStyle.FixedSingle;
                brilhoOn = !brilhoOn;
                brilhoCount++;

                if (brilhoCount > 5)
                {
                    brilhoTimer.Stop();
                    brilhoTimer.Dispose();
                }
            };

            brilhoTimer.Start();
        }

        private void Carta_Click(object sender, EventArgs e)
        {
            if (jogoEncerrado) return;
            if (escolhasRestantes <= 0) return;
            if (inputTravado) return;

            PictureBox pb = sender as PictureBox;
            if (pb == null || !pb.Enabled) return;

            inputTravado = true;
            pb.Enabled = false;

            Carta cartaEscolhida = pb.Tag as Carta;
            if (cartaEscolhida == null)
            {
                inputTravado = false;
                return;
            }

            AplicarCartaAnimada(cartaEscolhida, pb);
        }

        private void AplicarCartaAnimada(Carta carta, PictureBox pb)
        {
            if (jogoEncerrado) return;

            Timer fadeOut = new Timer { Interval = 50 };
            fadeOut.Tick += (s, e) =>
            {
                if (jogoEncerrado)
                {
                    fadeOut.Stop();
                    fadeOut.Dispose();
                    return;
                }

                pb.Visible = false;
                fadeOut.Stop();
                fadeOut.Dispose();

                AplicarCarta(carta);

                pb.Image = Properties.Resources.card_blank;
                pb.Visible = true;
                pb.Enabled = false;

                escolhasRestantes--;

                int indiceMesa = -1;
                if (pb.Parent == panel1) indiceMesa = 0;
                else if (pb.Parent == panel2) indiceMesa = 1;
                else if (pb.Parent == panel3) indiceMesa = 2;
                else if (pb.Parent == panel4) indiceMesa = 3;

                if (indiceMesa >= 0) cartasMesa[indiceMesa] = null;

                fugiuNaRodadaAnterior = false;

                AtualizarUI();
                AtualizarCartasRestantes();
                ChecarVitoria();

                inputTravado = false;

                if (jogoEncerrado) return;

                if (escolhasRestantes == 0)
                {
                    Timer delay = new Timer { Interval = 500 };
                    delay.Tick += (ss, ee) =>
                    {
                        delay.Stop();
                        delay.Dispose();
                        if (!jogoEncerrado) MostrarCartasAnimadas();
                    };
                    delay.Start();
                }
            };
            fadeOut.Start();
        }

        private void AplicarCarta(Carta carta)
        {
            if (jogoEncerrado) return;

            switch (carta.Naipe)
            {
                case "Diamonds": // ARMA
                    if (equipamentoAtual != null)
                        TocarSomQuebrarEquipamento();

                    equipamentoAtual = carta;
                    valorArma = carta.Valor;

                    ultimoMonstroComArma = int.MaxValue;
                    cartasHistorico.Clear();

                    AtualizarEquipamentoPanel();
                    AtualizarHistoricoPanel();

                    TocarSomSelecionarEquipamento();
                    break;

                case "Hearts": // POÇÃO (1 por sala)
                    {
                        if (pocaoUsadaNoTurno)
                        {
                            MessageBox.Show("Você já usou uma poção nessa sala!");
                            break;
                        }

                        int vidaAntes = vidaJogador;

                        vidaJogador += carta.Valor;
                        if (vidaJogador > 20) vidaJogador = 20;

                        pocaoUsadaNoTurno = true;

                        if (vidaJogador > vidaAntes)
                        {
                            TocarSomPoção();
                            PiscarVidaVerde();
                        }
                        else
                        {
                            MessageBox.Show("Vida cheia!");
                        }

                        break;
                    }

                case "Clubs":
                case "Spades":
                    {
                        int monstro = carta.Valor;
                        int dano;

                        bool temArma = (equipamentoAtual != null);
                        bool podeUsarArma = temArma && (monstro < ultimoMonstroComArma);

                        if (podeUsarArma)
                        {
                            dano = Math.Max(0, monstro - valorArma);

                            ultimoMonstroComArma = monstro;

                            cartasHistorico.Add(carta);
                            if (cartasHistorico.Count > 5) cartasHistorico.RemoveAt(0);
                            AtualizarHistoricoPanel();

                            TocarSomSelecionarEquipamento();
                        }
                        else
                        {
                            dano = monstro;
                            TocarSomDano();
                        }

                        if (dano > 0)
                        {
                            vidaJogador -= dano;
                            PiscarVidaVermelho();
                        }

                        if (vidaJogador <= 0)
                        {
                            GameOver();
                            return;
                        }

                        break;
                    }
            }

            score++;
        }

        private void PiscarVidaVermelho()
        {
            Color original = lblVida.ForeColor;
            Timer timer = new Timer { Interval = 150 };
            int count = 0;
            timer.Tick += (s, e) =>
            {
                if (jogoEncerrado)
                {
                    timer.Stop();
                    timer.Dispose();
                    lblVida.ForeColor = original;
                    return;
                }

                lblVida.ForeColor = lblVida.ForeColor == Color.Red ? original : Color.Red;
                count++;
                if (count > 3)
                {
                    timer.Stop();
                    timer.Dispose();
                    lblVida.ForeColor = original;
                }
            };
            timer.Start();
        }

        private void PiscarVidaVerde()
        {
            Color original = lblVida.ForeColor;
            Timer timer = new Timer { Interval = 150 };
            int count = 0;
            timer.Tick += (s, e) =>
            {
                if (jogoEncerrado)
                {
                    timer.Stop();
                    timer.Dispose();
                    lblVida.ForeColor = original;
                    return;
                }

                lblVida.ForeColor = lblVida.ForeColor == Color.LimeGreen ? original : Color.LimeGreen;
                count++;
                if (count > 3)
                {
                    timer.Stop();
                    timer.Dispose();
                    lblVida.ForeColor = original;
                }
            };
            timer.Start();
        }

        private void AtualizarUI()
        {
            lblVida.Text = $"Vida: {vidaJogador}";

            if (equipamentoAtual == null)
            {
                lblEquipamento.Text = "Arma: Nenhuma";
            }
            else
            {
                string limite = (ultimoMonstroComArma == int.MaxValue) ? "qualquer" : $"< {ultimoMonstroComArma}";
                lblEquipamento.Text = $"Arma: {equipamentoAtual.Nome} (Valor: {valorArma}) | Próximo monstro: {limite}";
            }

            lblScore.Text = $"Score: {score}";
        }

        private void AtualizarEquipamentoPanel()
        {

            panelEquipamento.Controls.Clear();

            if (equipamentoAtual != null)
            {
                PictureBox pb = new PictureBox
                {
                    Image = equipamentoAtual.Image ?? Properties.Resources.card_blank,
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    Dock = DockStyle.Fill,
                    BorderStyle = BorderStyle.None,          // ✅ remove borda
                    BackColor = Color.Transparent,
                    Margin = new Padding(0),
                    Padding = new Padding(0)
                };
                panelEquipamento.Controls.Add(pb);
            }
        }

        private void AtualizarHistoricoPanel()
        {
            for (int i = 0; i < painelHistorico.Length; i++)
            {
                painelHistorico[i].Controls.Clear();

                if (i < cartasHistorico.Count)
                {
                    PictureBox pb = new PictureBox
                    {
                        Image = cartasHistorico[i].Image ?? Properties.Resources.card_blank,
                        SizeMode = PictureBoxSizeMode.StretchImage,
                        Dock = DockStyle.Fill,
                        BorderStyle = BorderStyle.None,   // 🔥 remove borda branca
                        BackColor = Color.Transparent,    // 🔥 deixa fundo limpo
                        Margin = new Padding(0),          // 🔥 remove espaçamento
                        Padding = new Padding(0)
                    };
                    painelHistorico[i].Controls.Add(pb);
                    painelHistorico[i].Visible = true;
                }
                else
                {
                    painelHistorico[i].Visible = false;
                }
            }
        }

        private void btnFuga_Click(object sender, EventArgs e)
        {
            if (jogoEncerrado) return;

            if (fugiuNaRodadaAnterior)
            {
                MessageBox.Show("Você não pode fugir duas vezes seguidas!");
                return;
            }

            if (escolhasRestantes == 3)
            {
                foreach (var carta in cartasMesa)
                    if (carta != null)
                        baralho1.Add(carta);
                //  Garantia: cartas já “usadas pela arma” (histórico) não podem voltar pro deck
                if (cartasHistorico != null && cartasHistorico.Count > 0)
                    baralho1.RemoveAll(c => cartasHistorico.Contains(c));

                // (Opcional) Se você quiser garantir que o equipamento nunca volte por acidente:
                if (equipamentoAtual != null)
                    baralho1.RemoveAll(c => object.ReferenceEquals(c, equipamentoAtual));

                baralho1 = baralho1.OrderBy(x => rnd.Next()).ToList();

                for (int i = 0; i < cartasMesa.Count; i++)
                    cartasMesa[i] = null;

                fugiuNaRodadaAnterior = true;

                TocarSomFuga();
                MostrarCartasAnimadas();
                AtualizarCartasRestantes();
            }
            else
            {
                MessageBox.Show("Você não pode fugir depois de escolher uma carta!");
            }
        }

        private void AtualizarCartasRestantes()
        {
            if (lblCartasRestantes1 != null)
                lblCartasRestantes1.Text = $": {baralho1.Count}";
        }

        private void GameOver()
        {
            if (jogoEncerrado) return;

            jogoEncerrado = true;
            inputTravado = true;
            escolhasRestantes = 0;

            // congela UI principal
            panel1.Enabled = false;
            panel2.Enabled = false;
            panel3.Enabled = false;
            panel4.Enabled = false;
            btnFuga.Enabled = false;

            TocarSomMorte();

            MessageBox.Show($"Game Over! Seu Score final é: {score}");

            Timer timer = new Timer { Interval = 1000 };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                Application.Restart();
            };
            timer.Start();
        }

        private void btnSair_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        // Áudio
        private void TocarSFX(UnmanagedMemoryStream somStream)
        {
            if (somStream == null) return;

            Task.Run(() =>
            {
                try
                {
                    using (var player = new SoundPlayer(somStream))
                        player.PlaySync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Erro ao tocar SFX: {ex.Message}");
                }
            });
        }

        private void TocarSomFuga() => TocarSFX(Properties.Resources.somFuga);
        private void TocarSomQuebrarEquipamento() => TocarSFX(Properties.Resources.somQuebrarEquipamento);
        private void TocarSomSelecionarEquipamento() => TocarSFX(Properties.Resources.somEquipamentoSelecionado);
        private void TocarSomDano() => TocarSFX(Properties.Resources.somDano);
        private void TocarSomMorte() => TocarSFX(Properties.Resources.somMorte);
        private void TocarSomCompletarBaralho() => TocarSFX(Properties.Resources.somCompletarBaralho);
        private void TocarSomPoção() => TocarSFX(Properties.Resources.somPoção);
    }
}
