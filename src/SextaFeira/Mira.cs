using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Numerics;

namespace SextaFeira;

/// <summary>Lê os códigos de mira do CS2 (formato novo "CS...") e do Valorant pra desenhar a prévia.</summary>
internal static class Mira
{
    /// <summary>Uma linha da mira, em pixels a partir do centro (já na escala de 1080p).</summary>
    public record Traco(float Inicio, float Comprimento, float Espessura, Color Cor, bool Contorno, float ContornoEsp, Color CorContorno, bool SemTopo);

    public class Desenho
    {
        public List<Traco> Tracos = new();
        public bool Ponto;
        public float PontoTam;
        public Color PontoCor;
        public bool PontoContorno;
        public float ContornoEsp = 1;
        public Color CorContorno = Color.Black;
        public bool Circulo;
        public float CirculoRaio;
        public Color CirculoCor;
    }

    // ================= CS2 (formato novo, desde 30/09/2026) =================

    private const string Dicionario = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefhijkmnopqrstuvwxyz23456789";

    public static bool CodigoCs2Valido(string codigo) => LerBytesCs2(codigo) != null;

    private static byte[] LerBytesCs2(string codigo)
    {
        if (codigo == null || codigo.Length != 46 || !codigo.StartsWith("CS")) return null;
        BigInteger n = BigInteger.Zero;
        for (int i = codigo.Length - 1; i >= 2; i--)
        {
            int v = Dicionario.IndexOf(codigo[i]);
            if (v < 0) return null;
            n = n * 57 + v;
        }
        var b = n.ToByteArray(isUnsigned: true, isBigEndian: true);
        if (b.Length > 32) return null;
        var bytes = new byte[32];
        Array.Copy(b, 0, bytes, 32 - b.Length, b.Length);
        int soma = 0;
        for (int i = 1; i < 32; i++) soma += bytes[i];
        if (bytes[0] != (soma & 0xFF) || bytes[1] != 1) return null;
        return bytes;
    }

    public static Desenho LerCs2(string codigo)
    {
        var b = LerBytesCs2(codigo);
        if (b == null) return null;

        int alturaTela = Math.Max(240, b[2] | (b[3] << 8));
        float escala = 1080f / alturaTela; // tamanhos são em pixels da tela em que a mira foi feita
        int estilo = Math.Min(b[4] & 0x1F, 9);
        bool ponto = (b[4] & 0x40) != 0;
        bool estiloT = (b[4] & 0x80) != 0;
        var cor = Color.FromArgb(b[8], b[5], b[6], b[7]);
        var corContorno = Color.FromArgb(b[12], b[9], b[10], b[11]);
        int contorno = Math.Min(b[13] >> 6, 2);
        int espessura = Math.Min(b[13] & 0x3F, 32);
        int gap = (short)(b[14] | (b[15] << 8));
        int comprimento = b[16];

        var d = new Desenho { CorContorno = corContorno, ContornoEsp = 1 * escala };
        float esp = Math.Max(1, espessura) * escala;

        if (estilo is 1 or 3) // círculo
        {
            d.Circulo = true;
            d.CirculoRaio = Math.Max(3, (gap + comprimento) * escala);
            d.CirculoCor = cor;
        }
        else if (estilo != 6) // cruz (estilos clássicos, estáticos, quad, quadrado)
        {
            float inicio = Math.Max(0, gap * escala + esp / 2f);
            d.Tracos.Add(new Traco(inicio, comprimento * escala, esp, cor, contorno > 0, 1 * escala, corContorno, estiloT));
        }

        if (ponto || estilo == 6)
        {
            d.Ponto = true;
            d.PontoTam = esp;
            d.PontoCor = cor;
            d.PontoContorno = contorno > 0;
        }
        return d;
    }

    // ================= Valorant =================

    private static readonly Color[] CoresValorant =
    {
        Color.White, Color.FromArgb(0, 255, 0), Color.FromArgb(127, 255, 0), Color.FromArgb(223, 255, 0),
        Color.Yellow, Color.Cyan, Color.FromArgb(255, 0, 255), Color.Red
    };

    public static Desenho LerValorant(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo) || !codigo.StartsWith("0;")) return null;
        var t = codigo.Split(';');
        var primaria = new Dictionary<string, string>();
        string secao = "";
        for (int i = 1; i < t.Length; i++)
        {
            if (t[i] is "P" or "A" or "S") { secao = t[i]; continue; }
            if (i + 1 >= t.Length) break;
            if (secao == "P") primaria[t[i]] = t[i + 1];
            i++;
        }

        float F(string k, float padrao) =>
            primaria.TryGetValue(k, out var v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : padrao;
        bool B(string k, bool padrao) => primaria.TryGetValue(k, out var v) ? v == "1" : padrao;

        Color cor = Color.White;
        int ic = (int)F("c", 0);
        if (ic == 8 && primaria.TryGetValue("u", out var hex) && hex.Length >= 6)
        {
            try
            {
                cor = Color.FromArgb(
                    int.Parse(hex[..2], NumberStyles.HexNumber),
                    int.Parse(hex[2..4], NumberStyles.HexNumber),
                    int.Parse(hex[4..6], NumberStyles.HexNumber));
            }
            catch { }
        }
        else if (ic >= 0 && ic < CoresValorant.Length) cor = CoresValorant[ic];

        Color ComAlfa(float a) => Color.FromArgb((int)(Math.Clamp(a, 0, 1) * 255), cor);

        bool contorno = B("h", true);
        float contEsp = F("t", 1);
        var corCont = Color.FromArgb((int)(Math.Clamp(F("o", 0.5f), 0, 1) * 255), 0, 0, 0);

        var d = new Desenho { ContornoEsp = contEsp, CorContorno = corCont };

        if (B("d", false))
        {
            d.Ponto = true;
            d.PontoTam = F("z", 2);
            d.PontoCor = ComAlfa(F("a", 1));
            d.PontoContorno = contorno;
        }
        if (B("0b", true))
            d.Tracos.Add(new Traco(F("0o", 3), F("0l", 6), F("0t", 2), ComAlfa(F("0a", 0.8f)), contorno, contEsp, corCont, false));
        if (B("1b", true))
            d.Tracos.Add(new Traco(F("1o", 10), F("1l", 2), F("1t", 2), ComAlfa(F("1a", 0.35f)), contorno, contEsp, corCont, false));
        return d;
    }
}

/// <summary>Quadradinho que desenha a mira, ampliada pra dar pra ver.</summary>
internal class PreviaMira : Control
{
    private Mira.Desenho desenho;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Mira.Desenho Desenho
    {
        get => desenho;
        set { desenho = value; Invalidate(); }
    }

    public PreviaMira()
    {
        DoubleBuffered = true;
        Size = new Size(110, 110);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        // fundo "de jogo": cinza azulado com leve gradiente
        using (var fundo = new LinearGradientBrush(ClientRectangle, Color.FromArgb(70, 78, 92), Color.FromArgb(38, 42, 52), 90f))
            g.FillRectangle(fundo, ClientRectangle);

        float cx = Width / 2f, cy = Height / 2f;
        if (desenho == null)
        {
            TextRenderer.DrawText(g, "sem prévia", Tema.Pequena, ClientRectangle, Tema.Apagado,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        const float zoom = 3f; // amplia pra dar pra ver os detalhes
        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        void Retangulo(float x, float y, float w, float h, Color cor, bool contorno, float contEsp, Color corCont)
        {
            if (w <= 0 || h <= 0) return;
            if (contorno && corCont.A > 0)
            {
                float c = Math.Max(1, contEsp * zoom);
                using var bc = new SolidBrush(corCont);
                g.FillRectangle(bc, x - c, y - c, w + 2 * c, h + 2 * c);
            }
            using var b = new SolidBrush(cor);
            g.FillRectangle(b, x, y, w, h);
        }

        foreach (var t in desenho.Tracos)
        {
            float ini = t.Inicio * zoom, comp = t.Comprimento * zoom, esp = Math.Max(1, t.Espessura * zoom);
            // direita, esquerda, baixo, cima
            Retangulo(cx + ini, cy - esp / 2, comp, esp, t.Cor, t.Contorno, t.ContornoEsp, t.CorContorno);
            Retangulo(cx - ini - comp, cy - esp / 2, comp, esp, t.Cor, t.Contorno, t.ContornoEsp, t.CorContorno);
            Retangulo(cx - esp / 2, cy + ini, esp, comp, t.Cor, t.Contorno, t.ContornoEsp, t.CorContorno);
            if (!t.SemTopo)
                Retangulo(cx - esp / 2, cy - ini - comp, esp, comp, t.Cor, t.Contorno, t.ContornoEsp, t.CorContorno);
        }

        if (desenho.Circulo)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float r = desenho.CirculoRaio * zoom;
            using var p = new Pen(desenho.CirculoCor, 2);
            g.DrawEllipse(p, cx - r, cy - r, 2 * r, 2 * r);
        }

        if (desenho.Ponto)
        {
            float s = Math.Max(2, desenho.PontoTam * zoom);
            Retangulo(cx - s / 2, cy - s / 2, s, s, desenho.PontoCor, desenho.PontoContorno, desenho.ContornoEsp, desenho.CorContorno);
        }
    }
}

/// <summary>Bandeira do país (baixada uma vez de flagcdn.com e guardada no PC).</summary>
internal static class Bandeiras
{
    private static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static readonly Dictionary<string, Image> memoria = new();

    public static async Task<Image> Carregar(string pais)
    {
        if (string.IsNullOrEmpty(pais)) return null;
        pais = pais.ToLowerInvariant();
        lock (memoria) if (memoria.TryGetValue(pais, out var img)) return img;

        string pasta = Path.Combine(Sistema.PastaDados, "bandeiras");
        string arq = Path.Combine(pasta, pais + ".png");
        try
        {
            if (!File.Exists(arq))
            {
                Directory.CreateDirectory(pasta);
                var bytes = await http.GetByteArrayAsync($"https://flagcdn.com/w40/{pais}.png");
                await File.WriteAllBytesAsync(arq, bytes);
            }
            using var ms = new MemoryStream(await File.ReadAllBytesAsync(arq));
            var imagem = Image.FromStream(ms);
            var copia = new Bitmap(imagem);
            lock (memoria) memoria[pais] = copia;
            return copia;
        }
        catch { return null; }
    }
}
