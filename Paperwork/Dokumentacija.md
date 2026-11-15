# Swarm: dokumentacija razvoja projekta

Swarm je skupni projektni prostor za sodelovanje ljudi in agentov umetne inteligence. Namen projekta je povezati razvojne razprave, dokumentacijo, naloge in agentne seje na različnih računalnikih. Discord predstavlja glavno okolje za skupno komunikacijo, osrednji strežnik usklajuje podatke in povezave, spletna nadzorna plošča pa omogoča pregled delovanja sistema.

Dokument nadgrajuje [začetni predlog projekta](Proposal.md) in razvoj obravnava v petih fazah: analiza, načrtovanje, implementacija, testiranje in vzdrževanje. Faze se izvajajo iterativno: ob razvoju posamezne funkcionalnosti ponovno preverimo zahteve, dopolnimo načrt, izvedemo spremembe in preverimo njihovo delovanje. Opis ločuje trenutno izvedene funkcionalnosti od načrtovanih razširitev.

## 1. Analiza

### Opredelitev problema in uporabnikov

Izhodišče projekta so težave pri razvoju obsežnejšega sistema Slate LLM. Projektne informacije nastajajo v različnih pogovorih, razvojnih okoljih in agentnih sejah. Posledično je treba isti kontekst večkrat pojasnjevati, odločitve je težko ponovno poiskati, pregled nad odprtimi nalogami pa zahteva dodatno ročno delo.

Swarm naslavlja potrebo po skupnem prostoru, v katerem so zahteve, razprave, odločitve in rezultati povezani s konkretnim projektom. Predvideni uporabniki so lastniki projektov, sodelavci in skrbniki sistema. Agenti so izvajalci oziroma pomočniki, ki do podatkov in nalog dostopajo v okviru dovoljenj svojih lastnikov.

### Funkcionalne zahteve

Za ciljno različico prototipa so opredeljene naslednje zahteve:

- **FZ-01 – Projektni kontekst:** uporabniki in pooblaščeni agenti lahko poiščejo ter preberejo projektne razprave, zahteve in dokumentacijo.
- **FZ-02 – Povezovanje sej:** uporabnik lahko lokalno namestitev poveže s svojim računom Discord ter registrira in odklopi več agentnih sej.
- **FZ-03 – Dodeljevanje dela:** dovoljen uporabnik lahko zahtevo naslovi na določeno povezano sejo; navaden pogovor sam po sebi ne sproži dela.
- **FZ-04 – Sodelovanje agentov:** dovoljena seja lahko drugi seji pošlje vprašanje ali zahtevo, povezano s konkretno nalogo.
- **FZ-05 – Sledljivost:** pri zahtevah in objavah so razvidni pobudnik, izvajalna seja in povezana naloga.
- **FZ-06 – Spremljanje nalog:** sistem vodi stanje delovnih postavk; zaključek agentovega odgovora še ne pomeni samodejnega zaključka naloge.
- **FZ-07 – Pregled delovanja:** nadzorna plošča prikazuje stanje povezav in diagnostiko, pozneje pa tudi dejavnost sej ter napredek nalog.

Te zahteve opisujejo ciljni obseg. Trenutno sta izvedena osnovno povezovanje in registracija iz FZ-02 ter diagnostični del FZ-07. Preostale zahteve še niso v celoti implementirane.

### Nefunkcionalne zahteve in omejitve

Sistem mora preverjati identiteto povezane namestitve ter ločevati pravico do branja informacij od pravice do sprožanja dela. Skrivnosti ne smejo biti vključene v javno izvorno kodo ali običajne izpise. Odjemalci morajo ob prekinitvi povezave omogočiti ponovno povezovanje, uporabniški vmesnik pa mora nedosegljivost podatkov jasno prikazati.

Zasnova mora omogočati ločen razvoj strežnika, odjemalca in nadzorne plošče ter poznejše dodajanje agentnih okolij. Pri uporabi na Raspberry Pi je treba preveriti porabo pomnilnika, procesorja in omrežja; sprejemljive meje še niso določene ali izmerjene.

Prva različica je omejena na Discord in povezovanje že odprtih agentnih sej. Možnosti posredovanja sporočil so odvisne od posameznega agentnega okolja in jih je treba preveriti pred obljubo podpore. Trajna hramba, popolno upravljanje dovoljenj in izvajanje nalog še niso del trenutne implementacije.

### Rezultat faze

Rezultat analize so opredeljen problem, uporabniške vloge, funkcionalne in nefunkcionalne zahteve ter obseg prototipa. Uspešnost končnega prototipa bo ocenjena s potekom, v katerem uporabnik v Discordu oblikuje zahtevo, jo dodeli povezani seji, agent pridobi ustrezni kontekst in objavi sledljiv rezultat. Sodelovanje dveh sej bo preverjeno tudi ob dovoljenih in zavrnjenih zahtevah.

## 2. Načrtovanje

### Arhitektura sistema

Sistem je zasnovan kot osrednji strežnik z ločenim lokalnim odjemalcem in spletno nadzorno ploščo. Strežnik je modularna aplikacija: integracija Discord, komunikacijske končne točke in skupne storitve so deli istega procesa. Posamezni vstopni vmesniki uporabljajo skupna pravila, zato se logika registracije in preverjanja lastništva ne podvaja v vsakem kontrolerju.

Osrednji strežnik uporablja C# in ASP.NET Core. Discord je povezan prek knjižnice NetCord. Lokalni povezovalni odjemalec je izdelan v Pythonu, nadzorna plošča pa v Reactu in TypeScriptu z orodjema Vite in Tailwind CSS. S tem je začetna izbira Next.js iz predloga projekta nadomeščena z dejansko uporabljeno rešitvijo. Za trajno hrambo je predviden SQLite, vendar še ni vključen v izvajanje aplikacije.

Lokalni odjemalec sam vzpostavi odhodno povezavo WebSocket do strežnika. Ena lokalna storitev lahko registrira več sej in za posamezen naslov strežnika deli eno overjeno povezavo. Nadzorna plošča trenutno pridobiva diagnostične posnetke z zahtevami HTTP; lasten tok sprotnih dogodkov zanjo je načrtovana razširitev.

### Podatki in komunikacijski dogovori

Trenutni model loči identiteto namestitve, registrirano sejo in omrežno povezavo. Identiteta vsebuje lastnika ter identifikator in ime računalnika. Seja vsebuje svoj identifikator, pripadajočo povezavo, ime, oznako projekta, agentno okolje, način delovanja in čas registracije. Diagnostika ločeno vodi povezave, števce sporočil in meritve trajanja pošiljanja.

Za nadaljnji razvoj so predvideni podatki o projektih, delovnih postavkah, zahtevah, dovoljenjih in zgodovini dogodkov. Začetna skica podatkovnega modela je shranjena v [Database.drawio.xml](Database.drawio.xml); ne predstavlja že nameščene podatkovne zbirke.

Protokol sej uporablja sporočila JSON z različico, identifikatorjem zahteve, vrsto sporočila in vsebino. Odgovor se poveže z zahtevo prek njenega identifikatorja. Trenutne operacije so registracija, odregistracija, seznam sej in preverjanje odzivnosti. Lastništvo strežnik določi iz overjene identitete, ne iz poljubnega podatka v sporočilu. Podrobni dogovor je zapisan v [dokumentaciji odjemalca](../Client/README.md).

Povezovanje namestitve poteka v treh korakih: lokalni začetek postopka, potrditev prek ukaza Discord in lokalni zaključek s prejeto kodo. Za oddaljeno uporabo je predviden zaščiten prenos HTTPS oziroma WSS. Načina `solo` in `swarm` sta trenutno podatka registracije; pravila dodeljevanja dela na njuni podlagi še niso izvedena.

Za delovne postavke je v trenutni zasnovi predvideno zaporedje `open → in-progress → verification → closed`. Čakanje na uporabnikov odgovor se obravnava kot ločen podatek o dejavnosti seje oziroma zahteve. S tem je načrt stanj natančneje opredeljen kot v začetnem predlogu.

### Razdelitev odgovornosti in vrstni red razvoja

Tadej Gungl je odgovoren za jedro strežnika, komunikacijo in podatkovni del. Njegov dosedanji prispevek vključuje WebSocket, diagnostiko, izračune in povezavo diagnostičnih podatkov s stranjo Overview. Aleks Deticek je odgovoren za integracijo Discord; njegov dosedanji prispevek vključuje osnovno registracijo bota in ukaz `/ping`. Jan Vidovic je odgovoren za nadzorno ploščo, njeno postavitev, skupne komponente in zasnovo strani Admin. Podrobnosti posameznih prispevkov so ohranjene v tedenskih poročilih.

Razvoj poteka od povezljivosti in opazovanja sistema proti projektnim vsebinam in izvajanju nalog. Po stabilizaciji registracije sledijo trajna hramba in dovoljenja, nato dostop do konteksta, posredovanje zahtev v eno podprto agentno okolje ter sodelovanje več sej. Pri vsaki razširitvi je treba določiti tudi odziv ob napaki in način preverjanja.

### Rezultat faze

Rezultat načrtovanja so določene odgovornosti sestavnih delov, podatkovni in komunikacijski dogovori, zasnova uporabniških potekov ter vrstni red izvedbe. Načrt se dopolnjuje glede na ugotovitve implementacije in omejitve zunanjih vmesnikov.

## 3. Implementacija

### Osrednji strežnik in integracija Discord

Strežnik v mapi `Backend/Swarm.Server` vsebuje kontrolerje HTTP in WebSocket ter skupne storitve. `PairingService` vodi povezovanje namestitev in overjanje poverilnic. `SessionRegistry` upravlja registrirane seje in preverja pripadnost povezavi oziroma namestitvi. Storitve so registrirane prek mehanizma za podajanje odvisnosti; skupne zbirke v pomnilniku so zaščitene pred sočasnim spreminjanjem z `lock`.

Končni točki `/api/pairing/start` in `/api/pairing/complete` omogočata začetek in zaključek povezovanja. Ukaz `/swarm-pair` v Discordu poveže postopek z uporabnikovo identiteto in mu zasebno vrne potrditveno kodo. Ukaz `/ping` predstavlja osnovni preizkus odziva bota. Končna točka `/ws/sessions` omogoča overjeno komunikacijo za registracijo več sej.

Diagnostiko izvaja `ConnectionDiagnostics`. Beleži aktivne in zaključene povezave, število sporočil, čas delovanja in trajanje uspešnih pošiljanj. `Statistics` vsebuje ponovno uporabne izračune, `DurationFormatter` pa oblikovanje trajanja. `GET /diagnostics` vrne posnetek podatkov v JSON. Ločena demonstracijska pot `/ws` še vedno pošilja pozdravna sporočila; ni enaka protokolu registracije sej.

### Lokalni odjemalec in nadzorna plošča

Odjemalec v mapi `Client` vsebuje lokalno storitev, ukazni vmesnik, shranjevanje nastavitev in poverilnic ter povezavo s strežnikom. Omogoča povezovanje namestitve z računom Discord, registracijo in odklop sej, pregled stanja ter ponovno vzpostavitev prekinjene povezave. Trenutni lokalni komunikacijski mehanizem uporablja uporabniško vtičnico Unix. Navodila za povezovanje obstoječe agentne seje vsebuje [veščina swarm-connect](../skills/swarm-connect/SKILL.md).

Frontend v mapi `Frontend` vsebuje navigacijo, skupne komponente, odzivno postavitev ter svetlo in temno temo. Stran Overview samodejno pridobiva dejanske diagnostične podatke. Ob neuspešni zahtevi ohrani zadnje znane podatke in prikaže napako. Stran Admin predstavlja zasnovo uporabniškega vmesnika s podatki za prikaz; še ne upravlja dejanskih agentnih nalog.

### Trenutni rezultat in nadaljnje delo

Rezultat trenutne implementacije je prototip komunikacijske osnove: namestitev je mogoče overiti, registrirati več sej in spremljati povezave. Strežniške registracije in poverilnice so shranjene v pomnilniku, zato se ob ponovnem zagonu izgubijo. Odjemalec svoje poverilnice hrani lokalno, vendar mora po izgubi strežniškega stanja ponovno opraviti povezovanje.

Trajna podatkovna zbirka, orodja MCP, dostop do projektnih niti, posredovanje pozivov agentom, celovita projektna dovoljenja in sodelovanje agentov še niso implementirani. Registrirana seja zato še ne pomeni, da ji je mogoče poslati razvojno nalogo. Prav tako še nista potrjeni podpora vsem načrtovanim agentnim okoljem in delovanje na Windows.

V skladu z dogovorom s profesorjem se napredek predstavlja tedensko, ob rokih za obvezne vaje pa se odda vsebina, povezana s tekočo temo. [Poročilo tretjih vaj](weekly/003-diagnostika.md) obravnava podatke, funkcije in module; [poročilo četrtih vaj](weekly/004-doc.md) pa razrede, objekte, sodelovanje in polimorfizem na konkretnih primerih iz Swarma.

## 4. Testiranje

### Namen in način preverjanja

Testiranje preverja skladnost implementacije z zahtevami ter obnašanje pri neveljavnih podatkih, zavrnjenih zahtevah in prekinitvah povezav. Ločujemo preverjanje posameznih pravil, integracijske teste med sestavnimi deli in ročno preverjanje celotnega uporabniškega poteka.

Za posamezen preizkus je treba zabeležiti začetno stanje oziroma vhod, izvedeno dejanje, pričakovani in dejanski rezultat ter ugotovljena odstopanja. Napaka se po odpravi ponovno preveri tudi glede vpliva na že delujoče funkcionalnosti.

### Obstoječa preverjanja in zabeleženi rezultati

Program `Backend/Swarm.Server.Tests/Program.cs` preverja odkrivanje ukazov Discord, potrjevanje povezovanja, vezavo na lastnika, enkratno uporabo kod, omejitev napačnih poskusov, potek veljavnosti in zamenjavo poverilnic. Pri registru sej preverja omejitev dostopa na lastnika, zavrnitev odstranitve prek druge povezave, preprečevanje prevzema identifikatorja seje in pravilno čiščenje po ponovni povezavi. Testna ura omogoča preverjanje poteka veljavnosti brez čakanja.

Ta program je bil v okviru priprave poročila četrtih vaj 4. 11. 2026 uspešno izveden z izhodno kodo `0`. Podrobnejši primeri in rezultati so zapisani v [004-doc.md](weekly/004-doc.md). Preverjanje odkrivanja ukazov je lokalno in ne potrjuje njihovega delovanja na dejanskem strežniku Discord.

V `Client/tests/test_connector.py` so pripravljeni tudi integracijski testi odjemalca, ki uporabljajo ločen testni strežnik ASP.NET Core s simulirano potrditvijo Discord. Frontend vsebuje teste obdelave stanja Admin in komponente za prikaz števila povezav. Rezultatov teh testov v tem dokumentu še ne navajamo kot potrjenih; obstoj testov ni enak zabeleženemu uspešnemu zagonu.

Ukazi za ponovitev obstoječih preverjanj iz korena repozitorija so:

```bash
dotnet run --project Backend/Swarm.Server.Tests
Client/.venv/bin/python -m unittest discover -s Client/tests -v
npm --prefix Frontend test
```

Zagon predpostavlja nameščeno razvojno okolje in odvisnosti. Navodila za pripravo odjemalca so v [Client/README.md](../Client/README.md). Odvisnosti frontenda namestimo z `npm --prefix Frontend ci`; razpoložljivi ukazi so določeni v [package.json](../Frontend/package.json).

### Nadaljnja preverjanja in merila uspešnosti

Ročno je treba preveriti dejansko povabilo bota, dostop do ukazov in zasebni odgovor v Discordu ter celoten postopek povezovanja. Za diagnostiko se preveri prikaz nove povezave, sprememba števcev ob sporočilih in premik povezave v zgodovino po prekinitvi. Za registracijo je pomembno, da odklop ene seje ne odstrani drugih sej ter da prekinitev omrežja ne povzroči napačnega lastništva ali podvojenih registracij.

Po implementaciji dodeljevanja dela bo treba preveriti pravilnega prejemnika, zavrnjena dovoljenja, sledljivost pobudnika in izvajalca ter obravnavo podvojenih zahtev. Sodelovanje agentov bo moralo imeti omejitve, ki preprečujejo nenadzorovano nadaljevanje pogovora. Na ciljni napravi je treba posebej izmeriti porabo virov in preveriti uporabnost nadzorne plošče; zaslon na dotik ostaja možnost za poznejšo predstavitev.

Rezultat faze so zabeleženi izidi preverjanj, seznam odkritih napak in znanih omejitev ter ponovljivo dokazilo za delovanje posamezne funkcionalnosti. Končni prototip bo potrjen šele po uspešni izvedbi celotnega uporabniškega poteka iz analize.

## 5. Vzdrževanje

### Obseg vzdrževanja

Vzdrževanje vključuje odpravljanje napak, posodobitve knjižnic in razvojnih orodij, prilagoditve spremembam Discorda in agentnih okolij ter dopolnjevanje dokumentacije. Ker je projekt še v razvoju, gre trenutno predvsem za vzdrževanje razvojnega prototipa; redno produkcijsko obratovanje še ni potrjeno.

Pri odkriti napaki se najprej zabeležijo okoliščine in koraki za ponovitev. Nato se določi prizadeti del sistema, pripravi popravek in izvedejo ustrezna preverjanja. Če sprememba vpliva na protokol, nastavitve ali uporabniški postopek, je treba hkrati posodobiti navodila. Pri spremembi komunikacijskega dogovora je treba upoštevati tudi združljivost različic strežnika in odjemalca.

### Obratovanje in varovanje podatkov

Trenutna diagnostika omogoča pregled povezav in števcev, vendar se njena zgodovina ob ponovnem zagonu ponastavi. Navodila morajo zato pojasniti pričakovano vedenje po prekinitvi ali ponovnem zagonu, vključno s ponovno registracijo sej in po potrebi ponovnim povezovanjem računa.

Poverilnice in okoljske nastavitve je treba hraniti ločeno od javne kode ter jih ob razkritju zamenjati. Za oddaljeno uporabo je treba vzdrževati tudi veljavno zaščiteno povezavo. Po uvedbi SQLite bo treba določiti postopek varnostnega kopiranja, preverjanje obnove in izvajanje migracij podatkovne sheme. Varnostne kopije podatkovne zbirke so načrt vzdrževanja, ne že izvedena funkcionalnost.

### Dokumentacija in nadaljnji razvoj

Navodila za namestitev, zagon in povezovanje se vzdržujejo v datotekah README ter v dokumentaciji povezovalne veščine. Tedenska poročila beležijo izvedene prispevke in njihovo povezavo z obveznostmi predmeta. Ob spremembi obsega projekta se posodobijo tudi ta dokument, zahteve in znane omejitve.

Modularna zasnova je podlaga za dodajanje drugih agentnih okolij in pozneje drugih komunikacijskih platform. Vsaka razširitev zahteva preverjanje dejanskih zmožnosti zunanjega vmesnika, dopolnitev pravil dostopa ter ustrezna integracijska preverjanja.

Rezultat faze vzdrževanja je dokumentiran in preverljiv sistem, pri katerem so spremembe sledljive, postopki uporabe ponovljivi in omejitve jasno navedene.
