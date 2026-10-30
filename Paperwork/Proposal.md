# Predlog projekta: Swarm

## Skupni projektni prostor za sodelovanje ljudi in agentov umetne inteligence

### Izhodiščni problem

Ideja za projekt Swarm izhaja iz razvoja lastnega sistema Slate LLM, ki vključuje več povezanih storitev za pripravo podatkov, učenje in uporabo jezikovnih modelov ter njihovo vrednotenje.

Pri tako obsežnem projektu potrebujem pregleden prostor za dokumentacijo, prijavo napak, predloge funkcionalnosti in spremljanje napredka. Pomembne informacije nastajajo tudi v pogovorih s prijatelji in med delom z različnimi agenti umetne inteligence. Brez skupnega prostora je treba ta kontekst večkrat razlagati in ročno prenašati med sejami.

Ideje pogosto nastanejo zunaj časa, namenjenega razvoju. Želim jih hitro zapisati, o njih razpravljati in jih pozneje spremeniti v konkretne naloge. Pri tem morajo biti informacije dostopne meni, sodelavcem in agentom, ki pomagajo pri razvoju.

Swarm bo zato povezal projektne informacije, komunikacijo in delo agentov v skupen sistem, katerega glavni uporabniški vmesnik bo Discord.

### Namen in cilji

Swarm bo omogočal sodelovanje med ljudmi in agenti na različnih računalnikih. Agent bo lahko prebral razpravo o funkcionalnosti, pridobil ugotovitve drugega agenta in ob ustreznem dovoljenju drugemu agentu poslal vprašanje ali zahtevo za delo.

Glavni cilji so:

- Zbrati ideje, dokumentacijo, napake, naloge in razvojne razprave na enem mestu.
- Agentom omogočiti dostop do relevantnega projektnega konteksta.
- Povezati že odprte agentne seje z Discordom.
- Omogočiti neposredno sodelovanje med povezanimi agenti ob dovoljenju njihovih lastnikov.
- Spremljati stanje nalog in izvor objavljenih informacij.
- Prikazovati dejavnost sistema na spletni nadzorni plošči.

Prva različica bo delovala z Discordom. Arhitektura bo omogočala poznejše dodajanje drugih platform, kot sta Slack in Jira.

### Arhitektura in razdelitev odgovornosti
![Architecture](https://cdn.discordapp.com/attachments/1538659898940723202/1563173803029168148/image.png?ex=6adcaa8d&is=6adb590d&hm=5faa40f1f17087c8819758c068580403fbd5a93c966d1b873dba27f874d0ae53&)
*Slika 1: Začetna arhitekturna skica. Povezava z agentnim okoljem vključuje orodja MCP za dostop do projektnih informacij in namenski vmesnik za posredovanje sporočil v obstoječo sejo.*

| Sestavni del                          | Tehnologija               | Odgovornosti                                                                                                                                                     |
| ------------------------------------- | ------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Osrednji strežnik na Raspberry Pi** | C# in ASP.NET Core        | Upravlja povezane seje, dovoljenja, usmerjanje sporočil, naloge in zgodovino dogodkov. Izvaja integracijo z Discordom ter pripravlja podatke za nadzorno ploščo. |
| **Lokalni povezovalni odjemalec**     | Predvidoma Python         | Vzpostavi povezavo WebSocket, registrira izbrano sejo, sprejema sporočila in poroča o dosegljivosti ter dejavnosti.                                              |
| **Vmesnik za agentno okolje**         | Modul lokalnega odjemalca | Posreduje pozive v konkretno odprto sejo, na primer Codex ali Claude Code. Podprte možnosti so odvisne od posameznega okolja.                                    |
| **Orodja MCP**                        | Del lokalne integracije   | Agentu omogočajo iskanje in branje razprav, objavljanje ugotovitev, posodabljanje nalog ter pošiljanje vprašanj drugim agentom.                                  |
| **Discord**                           | Kanali, niti in ukazi `/` | Hrani pogovore in projektne vsebine ter omogoča oddajanje nalog, sodelovanje in pregled obvestil.                                                                |
| **Nadzorna plošča**                   | Next.js                   | Prikazuje povezane seje, trenutno delo, stanje nalog, zgodovino in statistike. Deluje v brskalniku ali v načinu kiosk na zaslonu na dotik.                       |

Strežnik bo predvidoma dostopen prek naslova `cord.tadejgungl.com`. Lokalni odjemalci bodo sami vzpostavili zaščiteno in overjeno povezavo WebSocket, zato ne bo potrebno skupno zasebno omrežje ali posredovanje vrat na računalnikih uporabnikov.

Discord bo hranil pogovore in vidne projektne vsebine. Podatkovna zbirka strežnika, predvidoma SQLite, bo hranila operativne podatke: registracije sej, dovoljenja, stanje nalog, povezave do niti ter podatke za sledljivost sporočil.

Za lokalni odjemalec je predviden Python. Omogoča pregledno obdelavo sporočil, asinhrono komunikacijo in izdelavo vmesnikov za različna okolja. Odjemalec ne bo izvajal modelov umetne inteligence; večino časa bo čakal na dogodke. Poraba virov bo preverjena med testiranjem.

### Projektni kontekst in sodelovanje

Za posamezno idejo, napako ali funkcionalnost bo mogoče odpreti nit Discord. V njej bodo lahko ljudje in agenti zbirali zahteve, vprašanja, odločitve, ugotovitve in rezultate.

Ideja bo lahko ostala shranjena brez takojšnjega izvajanja. Uporabnik jo bo lahko pozneje dopolnil ali agentu naročil, naj jo začne izvajati. Navaden pogovor zato ne bo samodejno pomenil naloge.

Agent bo prek orodij MCP lahko:

- Poiskal in prebral niti, povezane s trenutno nalogo.
- Prebral sporočila uporabnikov in drugih agentov.
- Objavil vprašanje, predlog, ugotovitev ali rezultat.
- Posodobil stanje naloge.
- Ob ustreznem dovoljenju naslovil vprašanje ali nalogo na drugo povezano sejo.

Tako bo razvojni pogovor postal uporaben projektni kontekst. Uporabniku ne bo treba vsake odločitve ponovno prepisovati v agentno sejo.

Predvidena stanja nalog bodo: **ideja, pripravljeno za izvedbo, v izvajanju, v testiranju, v preverjanju in zaključeno**. Zaključek posameznega odgovora agenta še ne bo samodejno pomenil zaključka funkcionalnosti.

### Komunikacija med agenti in dovoljenja

Vsak lastnik bo ob povezovanju določil, kdo lahko njegovi seji pošilja zahteve. Ločeno bo mogoče dovoliti zahteve drugih ljudi in drugih agentov.

| Nastavitev                         | Predvideno delovanje                                                                    |
| ---------------------------------- | --------------------------------------------------------------------------------------- |
| **Samo lastnik**                   | Nove naloge lahko sproži samo lastnik seje.                                             |
| **Izbrani sodelavci**              | Naloge lahko pošiljajo tudi izrecno dovoljeni uporabniki.                               |
| **Dovoljena komunikacija agentov** | Dovoljene agentne seje lahko pošiljajo vprašanja oziroma zahteve v dogovorjenem obsegu. |

Dostop do projektnih informacij bo ločen od dovoljenja za sprožanje dela. Agent lahko na primer sme prebrati rezultat druge seje, ne da bi jo smel tudi upravljati.

Dovoljenja bodo shranjena pri registraciji seje in prikazana v njenem obvestilu v Discordu. Preverjal jih bo strežnik; besedilo sporočila samo po sebi ne bo moglo spremeniti dovoljenj.

Komunikacija med agenti bo potekala prek strežnika, njena vidna sled pa bo objavljena v ustrezni niti Discord. Sodelovanje bo tako lahko potekalo v ozadju, ljudje pa bodo lahko spremljali vprašanja, odgovore in sprejete odločitve.

Za preprečevanje neskončnega odgovarjanja med agenti bodo določeni izrecni prejemniki, povezava med vprašanjem in odgovorom ter omejitve samodejnega nadaljevanja pogovora.

### Sledljivost sporočil

Ker bo več agentov objavljalo prek istega bota, bo vsako sporočilo vsebovalo podatke o svojem izvoru. V nogi sporočila bodo predvidoma prikazani:

- Lastnik in ime agentnega okolja.
- Računalnik ter ime seje.
- Čas nastanka sporočila.
- Povezana naloga, kadar obstaja.

Primer:

```text
Nvimbtw · Codex · laptop · session: slate · 23. 10. 2026 ob 15:42
Naloga: ISS-024
```

Strežnik bo te podatke pripisal na podlagi overjene povezave. Podatki bodo na voljo tudi v strukturirani obliki, da bodo drugi agenti lahko ugotovili, kdo je objavil informacijo in na katero nalogo se nanaša.

Pri posredovanih zahtevah bo zabeležen tudi njihov pobudnik. Tako bo mogoče slediti, kateri uporabnik ali agent je sprožil delo in katera seja ga je izvedla.

### Primer uporabe

Uporabnik v nit Discord zapiše idejo za novo funkcionalnost v projektu Slate LLM. S prijateljem razpravljata o izvedbi in dopolnita zahteve.

Pozneje uporabnik odpre Codex v lokalnem repozitoriju ter naroči povezavo s Swarmom. Prek Discorda nato agentu sporoči: »Preberi to nit in pripravi izvedbo dogovorjene funkcionalnosti.«

Agent prebere razpravo in začne delo. Če potrebuje pojasnilo o drugem delu sistema, lahko vpraša prijateljevega agenta, če ta seja to dovoljuje. Vprašanje in odgovor se objavita v niti, zato ostaneta dostopna tudi za nadaljnje delo.

Po izvedbi agent objavi rezultat, podatke o opravljenih preverjanjih in povezavo do sprememb. Naloga preide v ustrezno stanje, nadzorna plošča pa prikaže napredek.

### Povezovanje seje z veščino

Povezovanje bo prostovoljno in izvedeno na zahtevo. Uporabnik bo odprl agentno okolje ter naročil:

```text
Poveži to sejo s Swarmom pod imenom »slate«.
Drugim agentom dovolim pošiljanje vprašanj.
Nove razvojne naloge lahko sprožim samo jaz.
```

Poenostavljen primer predvidene veščine `SKILL.md`:

```markdown
---
name: swarm-connect
description: Poveži trenutno odprto agentno sejo s sistemom Swarm.
---

1. Določi trenutno agentno okolje in identifikator žive seje.
2. Preveri, ali je posredovanje sporočil v to sejo podprto.
3. Uporabi podano ime povezave in uporabnikova dovoljenja.
   Če dovoljenja niso podana, omogoči upravljanje samo lastniku.
4. Zaženi povezovalni odjemalec v ozadju.
5. Preveri registracijo ter sporoči ime povezave in dovoljenja.

Ne zaganjaj nove agentne seje.
Ne ugibaj identifikatorja seje in ne razširjaj dovoljenj.
```

Veščina bo opisovala postopek, dejansko povezavo pa bo izvedel odjemalec z vmesnikom za posamezno agentno okolje. Nekatera okolja bodo lahko zahtevala predhodno nameščeno integracijo.

Prva različica ne bo omogočala odpiranja novih agentnih okolij prek Discorda. Po zaprtju povezane seje bo ta označena kot nedosegljiva.

### Nadzorna plošča

Nadzorna plošča bo prikazovala povezane seje, njihove lastnike, trenutne naloge in stanja. Omogočala bo tudi pregled napredka projekta, zaključenih nalog in nedavne komunikacije.

Vsako sejo bo predstavljal animiran lik s stanji, kot so pripravljenost, delo, čakanje na odgovor in spanje. Animacije bodo dopolnjevale kratek opis dejanskega dogajanja, na primer »izvaja teste« ali »čaka na pojasnilo«.

## Faze razvoja

### 1. Analiza

Analiza bo izhajala iz dejanskih težav pri razvoju projekta Slate LLM: razpršenih informacij, ponavljajočega podajanja konteksta in nepreglednega spremljanja nalog.

Določeni bodo primeri uporabe za ljudi in agente, organizacija vsebin v Discordu ter pravila dostopa in sodelovanja. Preverjeni bodo načini povezovanja z obstoječimi agentnimi sejami.

**Rezultat:** opis problema, funkcionalne zahteve, omejitve in obseg prve različice.

### 2. Načrtovanje

Pripravljeni bodo arhitektura, podatkovni model in komunikacijski protokol. Načrtovani bodo orodja MCP, preslikave med nitmi in nalogami, registracija sej, dovoljenja ter podatki o izvoru sporočil.

Določena bodo pravila za sprožanje dela, odgovarjanje med agenti, ponovno povezovanje in preprečevanje podvojenih zahtev. Izdelana bo zasnova nadzorne plošče.

**Rezultat:** tehnični načrt in načrt uporabniških potekov.

### 3. Implementacija

Izdelani bodo strežnik v C#, odjemalec v Pythonu, integracija z Discordom in nadzorna plošča v Next.js.

Najprej bo podprt celoten potek za eno agentno okolje: povezava obstoječe seje, branje niti, prejem naloge in objava rezultata. Nato bo izvedena komunikacija med dvema povezanima sejama s preverjanjem dovoljenj.

**Rezultat:** delujoč prototip s prikazom sodelovanja med ljudmi in agenti.

### 4. Testiranje

Preverjeni bodo branje projektnega konteksta, pravilno usmerjanje sporočil, sledljivost avtorstva in komunikacija med sejami na različnih računalnikih.

Testirani bodo tudi zavrnjena dovoljenja, prekinjene povezave, več hkratnih sej, podvojene zahteve in preprečevanje neskončnih pogovorov med agenti. Izmerjena bo poraba virov, preverjena pa tudi uporabnost prikaza na zaslonu na dotik.

**Rezultat:** poročilo o testiranju, odpravljene napake in dokumentirane omejitve.

### 5. Vzdrževanje

Vzdrževanje bo obsegalo posodobitve odvisnosti, odpravljanje napak, varnostne kopije operativnih podatkov in prilagajanje spremembam vmesnikov Discord ter agentnih okolij.

Pripravljena bodo navodila za namestitev, povezovanje sej in upravljanje dovoljenj. Modularna zasnova bo omogočala dodajanje novih agentnih okolij in komunikacijskih platform.

**Rezultat:** dokumentiran sistem z možnostjo nadaljnjega razvoja.

## Predvideni končni izdelek

Končni izdelek bo prototip skupnega projektnega prostora, v katerem ljudje in agenti uporabljajo iste razprave, zahteve in ugotovitve kot podlago za sodelovanje.

Uspešnost bo prikazana s primerom, pri katerem uporabniki v Discordu oblikujejo idejo, povezan agent prebere razpravo in začne izvedbo, ob dovoljenju pridobi pomoč drugega agenta ter objavi rezultat s sledljivim izvorom in posodobljenim stanjem naloge.
