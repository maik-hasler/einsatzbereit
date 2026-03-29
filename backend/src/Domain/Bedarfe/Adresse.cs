using Domain.Primitives;

namespace Domain.Bedarfe;

public sealed record Adresse
{
    public string Strasse { get; }
    public string Hausnummer { get; }
    public string Plz { get; }
    public string Ort { get; }

    public Adresse(string strasse, string hausnummer, string plz, string ort)
    {
        if (string.IsNullOrWhiteSpace(strasse))
            throw new DomainException("Straße darf nicht leer sein.");

        if (string.IsNullOrWhiteSpace(hausnummer))
            throw new DomainException("Hausnummer darf nicht leer sein.");

        if (string.IsNullOrWhiteSpace(plz) || plz.Length != 5)
            throw new DomainException("PLZ muss genau 5 Ziffern haben.");

        if (string.IsNullOrWhiteSpace(ort))
            throw new DomainException("Ort darf nicht leer sein.");

        Strasse = strasse;
        Hausnummer = hausnummer;
        Plz = plz;
        Ort = ort;
    }
}
