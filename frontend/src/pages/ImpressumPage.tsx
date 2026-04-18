export default function ImpressumPage() {
  return (
    <>
      <h1 className="mb-8 text-3xl font-bold">Impressum</h1>

      <section className="mb-8">
        <h2 className="mb-2 text-xl font-semibold">Angaben gemäß § 5 TMG</h2>
        <p className="text-gray-700 leading-relaxed">
          [Vor- und Nachname / Organisation]<br />
          [Straße und Hausnummer]<br />
          [PLZ und Ort]
        </p>
      </section>

      <section className="mb-8">
        <h2 className="mb-2 text-xl font-semibold">Kontakt</h2>
        <p className="text-gray-700 leading-relaxed">
          E-Mail: [deine@email.de]
        </p>
      </section>

      <section className="mb-8">
        <h2 className="mb-2 text-xl font-semibold">Verantwortlich für den Inhalt nach § 55 Abs. 2 RStV</h2>
        <p className="text-gray-700 leading-relaxed">
          [Vor- und Nachname]<br />
          [Adresse wie oben]
        </p>
      </section>

      <section className="mb-8">
        <h2 className="mb-2 text-xl font-semibold">Haftungsausschluss</h2>
        <h3 className="mb-1 text-lg font-medium">Haftung für Inhalte</h3>
        <p className="text-gray-700 leading-relaxed mb-4">
          Die Inhalte dieser Seiten wurden mit größter Sorgfalt erstellt. Für die Richtigkeit, Vollständigkeit und Aktualität der Inhalte können wir jedoch keine Gewähr übernehmen.
        </p>
        <h3 className="mb-1 text-lg font-medium">Haftung für Links</h3>
        <p className="text-gray-700 leading-relaxed">
          Unser Angebot enthält Links zu externen Webseiten Dritter, auf deren Inhalte wir keinen Einfluss haben. Für die Inhalte der verlinkten Seiten ist stets der jeweilige Anbieter verantwortlich.
        </p>
      </section>
    </>
  )
}
