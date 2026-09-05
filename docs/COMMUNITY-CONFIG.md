# Community-configuratie

Spotnet is deels een servergebonden applicatie. Spots, reacties en klachten reizen
decentraal over Usenet, maar de *curatie* daarvan — de witte en zwarte lijsten die bepalen
welke posters je client vertrouwt — wordt centraal geserveerd. Daarnaast praat de client
met een paar community-diensten: een feedbacksite, een log-upload, promotietabs en een
Newznab-index.

Tot en met 3.0.8 stonden al die verwijzingen hardgecodeerd in `Configuration.cs`. Ze staan
nu in één bestand dat de gebruiker kan aanpassen:

```
%LocalAppData%\Spotnet3\Data\community_config.json
```

Dit is het pad voor een via de installer geïnstalleerde Spotnet. Bij een losse build
staat het bestand in de actieve gegevensmap (`AppHelper.SettingsFolder`).

De meegeleverde standaardwaarden wijzen naar dezelfde infrastructuur als altijd. Een
installatie waar niemand aan draait, gedraagt zich dus precies zoals daarvoor.

## Waarom

Twee redenen.

De eerste is praktisch: als de community verhuist, of als er ooit een tweede community
naast komt te staan, is dat een configuratiewijziging in plaats van een nieuwe release.

De tweede gaat over open source. Het bezwaar tegen publicatie van de broncode is dat je
daarmee een ongemodereerde Spotnet de wereld in helpt. Met deze opzet is het omgekeerde
waar: moderatie staat standaard aan en wijst naar de bestaande lijsten. Wie forkt en die
lijsten weghaalt, doet dat expliciet en zichtbaar.

## Waar je het aanpast

**Bewerken → Instellingen → Community.**

Het paneel toont bovenaan welke community actief is en hoeveel entries er in de lijsten
zitten, met een knop om ze meteen bij te werken. Daaronder staan de newsgroups en de
moderatie-instellingen; de servers en de indexer zitten in uitklapbare secties.

De Newznab-API-sleutel wordt afgeschermd getoond (alleen de laatste vier tekens). Met
**Vervangen** voer je een eigen sleutel in.

Met **Toon raw** klap je een JSON-editor open waarin de volledige configuratie te zien en
te bewerken is. *Overnemen in de velden* valideert de JSON en vult het paneel ermee; pas
bij **Opslaan** wordt het weggeschreven.

**Exporteer profiel…** en **Importeer profiel…** schrijven en lezen hetzelfde JSON-bestand.
Zo kan een community een profiel publiceren dat gebruikers in één handeling laden.

## Wat er in staat

| Sectie | Inhoud |
| :--- | :--- |
| `newsgroups` | `spots`, `comments`, `reports`, `nzb` — zie [PROTOCOL.md](PROTOCOL.md) |
| `moderation` | de vier lijst-URL's, aan/uit, bijwerkinterval, moderatorsleutels, handtekeninginstellingen |
| `services` | feedbacksite, log-upload, meldpunt voor mislukte updates, promo-map |
| `indexer` | Newznab-server en API-sleutel |

De newsgroups zijn hier de bron van waarheid: bij het opstarten worden ze naar de
applicatie-instellingen geschreven, waar de rest van de client ze uitleest.

Ontbrekende secties krijgen hun standaardwaarde. Een bestand dat alleen één URL overschrijft
blijft dus geldig als er later secties bijkomen. Is het bestand onleesbaar, dan valt de
client terug op de ingebouwde standaardwaarden en logt een waarschuwing — hij start altijd.

Wat er bewust **niet** in staat: de update-URL's. Die horen bij de build, niet bij de
community, en blijven in `Configuration.cs` staan.

## Ondertekende lijsten

De lijsten komen binnen over gewoon HTTP en bepalen wie je client vertrouwt. Wie het pad of
de DNS kaapt, bepaalt daarmee de trust-lijst van elke client die hem gebruikt.

Een community kan dat dichtzetten door naast elke lijst een detached handtekening te
publiceren — `whitelist.csv.sig` naast `whitelist.csv` — met daarin een base64 RSA-SHA256
handtekening over de bytes van de lijst. In de client vul je de bijbehorende publieke
sleutel in bij `moderation.signaturePublicKeyXml`, in dezelfde XML-vorm als de bestaande
update-sleutel.

Het gedrag daarna:

| Situatie | Resultaat |
| :--- | :--- |
| Geen sleutel ingesteld | Niets wordt gecontroleerd; de lijst wordt gebruikt zoals altijd |
| Sleutel ingesteld, handtekening klopt | Lijst wordt gebruikt |
| Sleutel ingesteld, handtekening klopt niet | Lijst wordt **verworpen** — dat wijst op sabotage of een verkeerde sleutel |
| Sleutel ingesteld, geen `.sig` gevonden | Lijst wordt gebruikt, tenzij `requireSignedLists` aan staat |

Dit staat standaard uit, omdat de huidige server niet ondertekent. Het inschakelen is een
bewuste handeling en laat nooit stilletjes de lijsten van een community wegvallen die er
nog niet aan doet.
