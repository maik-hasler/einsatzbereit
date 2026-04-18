export default function DatenschutzPage() {
  return (
    <>
      <h1 className="mb-8 text-3xl font-bold">Datenschutzerklärung</h1>

      <section className="mb-8">
        <h2 className="mb-2 text-xl font-semibold">Verantwortliche Stelle</h2>
        <p className="text-gray-700 leading-relaxed">
          [Vor- und Nachname / Organisation]<br />
          [Straße und Hausnummer]<br />
          [PLZ und Ort]<br />
          E-Mail: [deine@email.de]
        </p>
      </section>

      <section className="mb-8">
        <h2 className="mb-2 text-xl font-semibold">Erhebung und Speicherung personenbezogener Daten</h2>
        <p className="text-gray-700 leading-relaxed mb-4">
          Beim Besuch dieser Website werden automatisch Informationen allgemeiner Natur erfasst (z.&nbsp;B. IP-Adresse, Browsertyp, Uhrzeit des Zugriffs). Diese Daten lassen keine Rückschlüsse auf Ihre Person zu und werden zur Sicherstellung des Betriebs ausgewertet.
        </p>
        <p className="text-gray-700 leading-relaxed">
          Bei der Registrierung und Anmeldung über unseren Authentifizierungsdienst (Keycloak) werden die von Ihnen eingegebenen Daten (z.&nbsp;B. Name, E-Mail-Adresse) gespeichert, um Ihnen die Nutzung der Plattform zu ermöglichen.
        </p>
      </section>

      <section className="mb-8">
        <h2 className="mb-2 text-xl font-semibold">Weitergabe von Daten</h2>
        <p className="text-gray-700 leading-relaxed">
          Eine Übermittlung Ihrer persönlichen Daten an Dritte findet nicht statt, es sei denn, dies ist zur Vertragsabwicklung erforderlich oder Sie haben ausdrücklich eingewilligt.
        </p>
      </section>

      <section className="mb-8">
        <h2 className="mb-2 text-xl font-semibold">Cookies</h2>
        <p className="text-gray-700 leading-relaxed">
          Diese Website verwendet technisch notwendige Cookies für die Authentifizierung (Session-Cookies). Es werden keine Tracking- oder Werbe-Cookies eingesetzt.
        </p>
      </section>

      <section className="mb-8">
        <h2 className="mb-2 text-xl font-semibold">Ihre Rechte</h2>
        <p className="text-gray-700 leading-relaxed">
          Sie haben das Recht auf Auskunft, Berichtigung, Löschung und Einschränkung der Verarbeitung Ihrer personenbezogenen Daten. Wenden Sie sich dazu an die oben genannte verantwortliche Stelle.
        </p>
      </section>
    </>
  )
}
