# Vaje 3: podatki, funkcije in moduli v projektu Swarm

## Prispevek k projektu

Dodana je diagnostika povezav WebSocket. Končna točka `GET /diagnostics` vrne čas delovanja diagnostike, število povezav in sporočil, trajanje posameznih povezav ter statistiko časa pošiljanja sporočil. Stran Overview na nadzorni plošči prikazuje te podatke in jih samodejno osvežuje.

Povezava WebSocket še ni overjena agentna seja. Trenutno strežnik približno vsako sekundo pošlje pozdrav, prejeta sporočila pa prešteje in njihovo vsebino zavrže.

## Podatki in funkcije

Implementacija je v `Backend/Swarm.Server/`:

- `Diagnostics/DurationFormatter.cs`: funkcija `Format(long totalSeconds)` vrne besedilo trajanja. Uporablja konstanti, celoštevilsko deljenje in ostanek pri deljenju. Ure niso omejene na 24. Negativen vhod sproži izjemo.
- `Diagnostics/Statistics.cs`: funkcije `Average(double[])`, `Maximum(double[])` in `CountAbove(double[], double)` obdelujejo meritve z zankami, brez LINQ in brez spreminjanja vhodnega polja.
- `Diagnostics/ConnectionDiagnostics.cs`: hrani podatke v pomnilniku, beleži povezave in sporočila ter sestavi posnetek trenutnega stanja.
- `Diagnostics/DiagnosticsSnapshot.cs`: določa strukturo podatkov, ki jih vrne API.
- `Controllers/DiagnosticsController.cs`: vrne posnetek kot odgovor HTTP; ne izvaja statističnih izračunov.
- `Controllers/WebSocketController.cs`: beleži uspešna pošiljanja, celotna prejeta sporočila ter odpiranje in zapiranje povezav.

Uporabljeni tipi vključujejo `long` za števce in sekunde, `double` za meritve v milisekundah, `string` za oblikovano trajanje, `Guid` za identifikator povezave ter `DateTimeOffset` za časovne oznake UTC. Tip `double?` dopušča tudi vrednost `null`, kadar izračun ni definiran. Pogoji primerjajo stanje povezave, konec sporočila in izmerjeno vrednost z mejo.

`Stopwatch` meri pretečeni čas neodvisno od sprememb sistemske ure. Čas pošiljanja je trajanje strežniške operacije `SendAsync`; ne meri zakasnitve do odjemalca in nazaj ter ne potrjuje prejema sporočila.

## Modul statistike

Razred `Statistics` je `public static`. `public` omogoča uporabo zunaj razreda, `static` pa klic brez ustvarjanja njegovega primerka, na primer `Statistics.Average(measurements)`. Modul nima konzolnega vnosa, izpisa ali skupnega spremenljivega stanja. Njegov rezultat je odvisen od podanih vhodov.

- `Average`: vhod je polje meritev; rezultat je vsota, deljena s številom meritev. Za prazno polje vrne `null`.
- `Maximum`: vhod je polje meritev; rezultat je največji element. Za prazno polje vrne `null`. Začetni maksimum je prvi element, zato funkcija deluje tudi za polje samih negativnih vrednosti.
- `CountAbove`: vhoda sta polje meritev in meja; rezultat je število elementov, strogo večjih od meje. Za prazno polje vrne `0`.

Vse tri metode pri vhodu `null` sprožijo `ArgumentNullException`. Pri dejanskih meritvah pošiljanja so vhodi nenegativne končne vrednosti, izmerjene s štoparico.

`ConnectionDiagnostics` dobi navadno polje `double[]` iz vrste zadnjih meritev in ga poda statističnemu modulu. Isti modul bi lahko uporabljal tudi ukaz Discord ali druga aplikacija.

## Povezava s celotnim sistemom

V `Program.cs` je `ConnectionDiagnostics` registriran z `AddSingleton`, zato kontrolerja HTTP in WebSocket uporabljata isti primerek. Ta storitev namenoma hrani skupno stanje; čisti računski funkciji in modul statistike ga ne hranijo.

Ker več povezav in zahtev HTTP teče sočasno, dostop do števcev in zbirk varuje `lock`. Vrnjeni posnetki vsebujejo kopije podatkov. V pomnilniku ostaja največ 100 zadnjih meritev pošiljanja in 20 zadnjih zaključenih povezav. Aktivne povezave ostajajo zabeležene, dokler se ne zaprejo. Skupni števci vključujejo tudi že zaprte povezave in se ponastavijo ob ponovnem zagonu.

Kontroler WebSocket hkrati pošilja in sprejema. Prejeto sporočilo šteje ob `EndOfMessage`, zato več fragmentov istega sporočila ne poveča števca večkrat. Zapiralno sporočilo ni podatkovno sporočilo. Ob koncu ene zanke se druga prekliče, obe se počakata, nato se povezava odstrani iz aktivnih in zapiše v zgodovino.

Frontend je izdelan z React, TypeScript, Vite in Tailwind CSS. `Frontend/src/lib/diagnostics.ts` opisuje podatkovne tipe odgovora API, `use-diagnostics.ts` skrbi za pridobivanje podatkov, `pages/OverviewPage.tsx` pa za prikaz. Po končani zahtevi počaka eno sekundo in nato pošlje naslednjo; zahteve se ne prekrivajo. Ob nedosegljivem strežniku prikaže napako, označi ohranjene podatke kot zadnje znane ter samodejno ponavlja zahteve. Ob odhodu s strani prekine aktivno zahtevo in časovnik.

Vite posreduje zahteve z `/api/diagnostics` na strežniško pot `/diagnostics`. Brskalnik sam ne odpira povezave WebSocket. Povezavo za predstavitev odpre terminalski odjemalec `websocat`, zato nadzorna plošča prikazuje njegovo dejavnost. Obstoječa stran Admin še uporablja vzorčne podatke in je tako tudi označena.

## Prispevki članov

### Tadej Gungl

Izdelal sem povezavo WebSocket, beleženje povezav in sporočil ter diagnostiko z izhodom HTTP. Za prikaz teme vaj sem izločil oblikovanje trajanja in statistične izračune v ponovno uporabne funkcije. Uporabljam podatkovne tipe, konstante, deljenje in ostanek pri deljenju, polja, zanke, pogoje, parametre in vrnjene vrednosti. Modul `Statistics` izvaja samo izračune, storitev `ConnectionDiagnostics` pa hrani stanje povezav.

Diagnostiko sem povezal z Janovo osnovo frontenda prek strani Overview in posredovanja zahtev v Vite. Tako je mogoče med predstavitvijo spremljati dejansko terminalsko povezavo na nadzorni plošči. Moj del sistema zagotavlja podatke in komunikacijo, ki jih lahko uporabniška vmesnika pozneje uporabljata za spremljanje agentnih sej.

### Aleks Deticek

Izdelal sem osnovno integracijo bota Discord z uporabo NetCord. Strežnik registrira Discord Gateway in module aplikacijskih ukazov. Razred `PingCommand` vsebuje ukaz `/ping`, katerega metoda `Execute()` vrne besedilni odgovor.

S tem predstavim funkcije oziroma metode, vrnjene vrednosti ter organizacijo kode v datoteke, razrede in imenske prostore. Ukaz je v ločenem modulu, njegova registracija pa v `Program.cs`. Moj del je uporabniški vstop v skupni strežnik prek Discorda; trenutni `/ping` še ne upravlja povezav WebSocket ali agentnih nalog.

### Jan Vidovic

Izdelal sem osnovo nadzorne plošče v React in TypeScript ter stran Admin z navigacijo, skupnimi komponentami, svetlo in temno temo ter odzivno postavitvijo. Admin prikazuje vzorčne seje, zahteve, zgodovino in delovne postavke.

Temo vaj predstavim s podatkovnimi tipi in polji, funkcijami za obdelavo podatkov ter razdelitvijo kode v module. `fixtures.ts` vsebuje vzorčne podatke, `admin-state.ts` funkcije za združevanje, štetje in spremembe stanja, komponente pa te rezultate prikažejo. Primeri so `workCounts`, `waitingCount`, `groupWorkItems` in `adminReducer`. Frontend uporablja tudi metode `filter` in `map`; zahtevo vaj po izračunih z lastnimi zankami posebej prikazuje strežniški modul `Statistics`.

Moje komponente, navigacijo in slogovne spremenljivke uporablja tudi nova stran Overview. Tako se osnova uporabniškega vmesnika poveže s Tadejevimi dejanskimi podatki strežnika, medtem ko Admin ostaja načrt uporabniškega vmesnika z vzorčnimi podatki.

## Primeri za poznejšo predstavitev

Spodaj so pričakovani rezultati, ne poročilo o izvedenih preverjanjih:

```csharp
DurationFormatter.Format(7322);   // "2 h 2 min 2 s"
DurationFormatter.Format(90061);  // "25 h 1 min 1 s"
DurationFormatter.Format(0);      // "0 h 0 min 0 s"

double[] measurements = { 18, 20, 19, 23, 20 };
Statistics.Average(measurements);         // 20
Statistics.Maximum(measurements);         // 23
Statistics.CountAbove(measurements, 20);  // 1

Statistics.Average(Array.Empty<double>());         // null
Statistics.Maximum(Array.Empty<double>());         // null
Statistics.CountAbove(Array.Empty<double>(), 20);  // 0
```

## Predstavitev z nadzorno ploščo in terminalom

1. V `Backend/Swarm.Server` zaženemo `dotnet run`. Za Discord mora biti nastavljen `DISCORD_TOKEN`; nalagalnik poišče tudi `Backend/.env`.
2. V `Frontend` po namestitvi odvisnosti z `npm ci` zaženemo `npm run dev` in odpremo `http://localhost:5173/overview`.
3. V drugem terminalu zaženemo `websocat ws://localhost:5142/ws`. Na nadzorni plošči se prikaže aktivna povezava. Strežnik približno vsako sekundo pošlje pozdrav, zato raste števec poslanih sporočil in trajanje povezave.
4. V `websocat` vpišemo vrstico in pritisnemo Enter. Na nadzorni plošči se poveča števec prejetih sporočil.
5. Zapremo terminalskega odjemalca. Povezava se premakne v nedavne prekinitve s končnim trajanjem in števci. Ob odprtju drugega odjemalca lahko pokažemo tudi več sočasnih povezav.
6. Aleks lahko v Discordu pokaže `/ping` in razloži pot od registriranega ukaza do vrnjenega odgovora. Jan predstavi komponente in obdelavo vzorčnih podatkov na strani Admin ter skupne elemente uporabniškega vmesnika.

Povprečje in maksimum sta pred prvim uspešnim pošiljanjem v API `null`; nadzorna plošča takrat prikaže pomišljaj. Števci so z vidika strežnika. Zahteve HTTP za diagnostiko se ne štejejo kot sporočila WebSocket. Spremljanje odjemalca ne potrebuje ročnega osveževanja strani.

Ta prikaz neposredno obravnava podatke, funkcije, časovne izračune, obdelavo meritev in module. Posebnih zahtev iz igre ugibanja in izračuna cene dostave ne predstavlja kot izvedenih.

## Stanje preverjanja

Na zahtevo uporabnika pomočnik ob implementaciji ni izvajal gradnje, testov ali zagona aplikacije. Zgornji primeri in potek predstavitve opisujejo pričakovano vedenje; izvedene predstavitve naj člani naknadno zabeležijo kot dejanska opažanja.
