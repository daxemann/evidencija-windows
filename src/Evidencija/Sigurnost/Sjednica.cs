using System.Security.Cryptography;

namespace Evidencija.Sigurnost;

/// <summary>Otključan ključ u memoriji (samo dok program radi) + kočnica za krive lozinke.</summary>
public sealed class Sjednica
{
    private readonly Kljucevi _kljucevi;
    private byte[]? _kljuc;
    private int _krivih;
    private DateTime _cekajDo = DateTime.MinValue;

    public Sjednica(Kljucevi kljucevi) => _kljucevi = kljucevi;

    public event Action? Promjena;

    public bool Otkljucano => _kljuc != null;
    public bool PrvoPokretanje => !_kljucevi.Postoji();

    /// <summary>Ključ podataka; baca iznimku ako program nije otključan.</summary>
    public byte[] Kljuc => _kljuc ?? throw new InvalidOperationException("Program je zaključan.");

    /// <summary>Koliko još sekundi treba čekati nakon više krivih lozinki (0 = može).</summary>
    public int CekajSekundi => Math.Max(0, (int)Math.Ceiling((_cekajDo - DateTime.Now).TotalSeconds));

    public void Postavi(string lozinka, string rezervna)
    {
        Otvori(_kljucevi.Kreiraj(lozinka, rezervna));
    }

    /// <summary>Prijava lozinkom (ili rezervnom). Vraća false za krivu lozinku.</summary>
    public bool Prijava(string vrsta, string lozinka)
    {
        if (CekajSekundi > 0)
        {
            return false;
        }
        var kljuc = _kljucevi.Otkljucaj(vrsta, lozinka);
        if (kljuc == null)
        {
            _krivih++;
            if (_krivih >= 5)
            {
                _cekajDo = DateTime.Now.AddSeconds(Math.Min(300, 30 * (_krivih - 4)));
            }
            return false;
        }
        _krivih = 0;
        Otvori(kljuc);
        return true;
    }

    public void PromijeniLozinku(string vrsta, string nova) => _kljucevi.Promijeni(Kljuc, vrsta, nova);

    /// <summary>Provjera trenutne lozinke bez promjene stanja (npr. prije promjene lozinke).</summary>
    public bool ProvjeriLozinku(string vrsta, string lozinka)
    {
        var k = _kljucevi.Otkljucaj(vrsta, lozinka);
        if (k == null)
        {
            return false;
        }
        CryptographicOperations.ZeroMemory(k);
        return true;
    }

    public void Zakljucaj()
    {
        if (_kljuc != null)
        {
            CryptographicOperations.ZeroMemory(_kljuc);
            _kljuc = null;
        }
        Promjena?.Invoke();
    }

    private void Otvori(byte[] kljuc)
    {
        _kljuc = kljuc;
        Promjena?.Invoke();
    }
}
