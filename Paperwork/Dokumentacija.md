# Swarm: dokumentacija projekta

Swarm je skupni projektni prostor za sodelovanje ljudi in agentov umetne inteligence. Osrednji strežnik v C# združuje komunikacijo, podatke in integracijo z Discordom, spletna nadzorna plošča pa prikazuje stanje sistema. Povezovanje z dejanskimi agentnimi sejami, dovoljenja in trajna hramba so naslednji koraki razvoja.

## Trenutno stanje

Strežnik ASP.NET Core vsebuje osnovno integracijo Discord z ukazom `/ping`, končno točko WebSocket `/ws` ter HTTP API `/diagnostics`. Diagnostika beleži aktivne in zaključene povezave, čas delovanja, števce sporočil in statistiko trajanja uspešnih pošiljanj. Podatki so shranjeni v pomnilniku in se ob ponovnem zagonu ponastavijo.

Frontend uporablja React, TypeScript, Vite in Tailwind CSS. Stran Overview prikazuje dejanske podatke diagnostike in jih samodejno osvežuje z zahtevami HTTP. Stran Admin prikazuje zasnovo upravljanja z vzorčnimi podatki; ti še niso povezani z agentnimi sejami ali Discord nalogami.

Za prikaz povezave uporabljamo terminalskega odjemalca `websocat`. Nadzorna plošča ga spremlja prek API in sama ne odpira dodatne povezave WebSocket. Strežnik pošilja pozdravna sporočila, prejeta podatkovna sporočila pa prešteje in zavrže njihovo vsebino. Ta prototip še ni protokol za izvajanje agentnih zahtev.

## Tadej Gungl

Moj trenutni prispevek obsega komunikacijo WebSocket, diagnostiko povezav, API za njene podatke in povezavo teh podatkov z nadzorno ploščo. Implementiral sem beleženje odpiranja in zapiranja povezav, trajanja in števila sporočil ter statistiko zadnjih meritev pošiljanja.

Izračune sem ločil od komunikacije: `DurationFormatter` oblikuje trajanje, `Statistics` izračuna povprečje, maksimum in število meritev nad mejo, `ConnectionDiagnostics` pa vodi stanje. `DiagnosticsController` vrne podatke v JSON. Na frontendu `useDiagnostics` pridobiva posnetke, `OverviewPage` pa jih prikaže z Janovimi skupnimi komponentami in temo.

Pri tedenskih vajah ta del predstavlja podatkovne tipe, konstante, parametre, vrnjene vrednosti, deljenje in ostanek pri deljenju, polja, zanke ter organizacijo v module. V celotnem sistemu je to osnova komunikacije in opazovanja poznejših agentnih sej.

## Aleks Deticek

Moj trenutni prispevek je osnovni bot Discord z uporabo knjižnice NetCord. Integracija v `Program.cs` registrira Discord Gateway in aplikacijske ukaze. Ukaz `/ping` je v ločenem razredu `PingCommand`; njegova metoda `Execute()` vrne besedilni odgovor.

Pri tedenskih vajah lahko razložim klice metod, vrnjene vrednosti, ločene datoteke, razrede in imenske prostore ter registracijo ukaznega modula. V celotnem sistemu Discord predstavlja uporabniški vmesnik za skupno komunikacijo. Upravljanje nalog, objavljanje napredka in povezava ukazov z agentnimi sejami so načrtovane razširitve.

## Jan Vidovic

Moj trenutni prispevek je osnova spletne nadzorne plošče: navigacija, stran Admin, skupne komponente, odzivna postavitev ter svetla in temna tema. Admin vključuje prikaz vzorčnih sej, vrste zahtev, zgodovine in skupin delovnih postavk.

Pri tedenskih vajah lahko pokažem tipe in polja v `fixtures.ts`, funkcije in reducer v `admin-state.ts` ter njihovo uporabo v komponentah. Funkcije združujejo in štejejo podatke, reducer pa na podlagi dogodka vrne naslednje stanje vmesnika. Podatki, obdelava in prikaz so razdeljeni v module.

V celotnem sistemu frontend omogoča pregled delovanja strežnika. Nova stran Overview uporablja mojo osnovo vmesnika za prikaz Tadejeve diagnostike. Stran Admin še uporablja vzorčne podatke in ne upravlja dejanske vrste zahtev.

## Tedenske oddaje in predstavitev

Podrobna povezava s temo tretjih vaj ter navodila za predstavitev so v [003-diagnostika.md](003-diagnostika.md). Izvirna navodila so v [003-vaje.md](003-vaje.md), začetni predlog projekta pa v [Proposal.md](Proposal.md).

Za predstavitev zaženemo strežnik in frontend, odpremo `/overview` ter v ločenem terminalu vzpostavimo povezavo z `websocat ws://localhost:5142/ws`. Spremljamo nastanek povezave, naraščanje števcev in trajanja ter zapis v zgodovino po prekinitvi. Vsak član predstavi svoj zgoraj opisani prispevek in njegovo povezavo z drugimi deli sistema.

Pomočnik na zahtevo uporabnika med temi spremembami ni izvajal gradnje, testov ali zagona aplikacije. Navodila opisujejo pripravljeno predstavitev; opažanja po izvedbi se v dokumentacijo dodajo ločeno.
