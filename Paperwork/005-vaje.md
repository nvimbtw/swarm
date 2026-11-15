# Vaje 5: povezovanje modulov v delujočo aplikacijo

*4. 11. 2026 · 3 šolske ure*

Ustvarite konzolni projekt `Vaja005` v C#. Nadaljevali boste primer knjige iz četrtega tedna. Tokrat boste povezali iskanje knjige, postopek izposoje in prikaz rezultata. Pomagajte si s [predavanjem](005-predavanje.md).

**Cilj je razumeti sodelovanje delov:** kdo prejme podatek, kdo preveri pravilo, kateri objekt se spremeni in kako rezultat pride do prikaza. Pri vsakem koraku rezultat najprej napovejte in ga nato preverite. Svojo kodo morate znati zagovarjati.

Zadostuje konzolni program z vnaprej pripravljenima knjigama in zaporedjem klicev. Vnos s tipkovnice, meniji, podatkovna zbirka, datoteke za shranjevanje in spletna povezava niso zahtevani. Koda naj ostane kratka in pregledna.

Individualni zagovor poteka po dogovoru z izvajalcem. Skupaj je mogoče doseči **100 točk: 70 za izdelavo in preverjanje ter 30 za razumevanje na zagovoru**.

## 1. Pripravi knjigo in določi odgovornosti — 10 točk

*Predavanje: poglavji 1 in 2. Okvirno 10 minut.*

V novi projekt prenesite svoj razred `Knjiga` iz vaj četrtega tedna. Potrebujete naslov, avtorja, stanje `JeIzposojena`, konstruktor in metodo `bool Izposodi()`. Ob prvi izposoji proste knjige metoda vrne `true`, ob ponovljeni `false`. Izpisa v razredu ni. Druge že izdelane člane lahko ohranite.

Če razreda še nimate dokončanega, uporabite podporni razred na koncu predavanja in ga preberite. To ne zmanjšuje točk: pri tej nalogi ocenjujemo pripravo za povezovanje in razumevanje, ne ponovne izdelave vaje 4.

Pripravite dva ločena objekta:

| Naslov              | Avtor         |
| ------------------- | ------------- |
| `Učimo se C#`       | `Ana Novak`   |
| `Osnove algoritmov` | `Miha Kranjc` |

Svojo kopijo razreda preizkusite z dvema zaporednima klicema `Izposodi()` na enem objektu. Za poznejše naloge nato pripravite nove, proste knjige.

V kratki preglednici zapišite po eno odgovornost za `Knjiga`, `KatalogVSpominu`, `StoritevIzposoje` in `Program`. Dodajte, kateri del bi spremenili za drugačno sporočilo uporabniku in kateri za drugačno iskanje.

**Na zagovoru:** pojasnite razliko med stanjem ene knjige in postopkom, ki knjigo najprej poišče. Zakaj izpis ne sodi v metodo knjige? Ali premik kode v več datotek že zagotovi smiselne module?

## 2. Zapiši dogovor med moduloma — 10 točk

*Predavanje: poglavji 3 in 4. Okvirno 10 minut.*

V datoteki `IKatalogKnjig.cs` napišite vmesnik z eno metodo:

```csharp
public interface IKatalogKnjig
{
    Knjiga? Poisci(string naslov);
}
```

V novem konzolnem projektu naj bodo vključeni ničelni referenčni tipi. Če je projekt podedovan iz starejše predloge, preverite nastavitev `<Nullable>enable</Nullable>` v datoteki projekta.

Ob vmesniku v komentarju zapišite ta dogovor s svojimi besedami: prejme neprazen naslov, na začetku išče z natančnim ujemanjem, vrne shranjeni objekt ali `null` in ne spremeni stanja knjige. Uporabljamo veljavne objekte z različnimi naslovi; seznam med prikazom ostane isti.

V datoteki `RezultatIzposoje.cs` napišite naštevni tip `RezultatIzposoje` s štirimi vrednostmi: `Uspela`, `NeveljavenNaslov`, `KnjigaNeObstaja`, `ZeIzposojena`.

**Na zagovoru:** pojasnite `Knjiga?`, pomen `null` in razliko med vmesnikom ter njegovo izvedbo. Zakaj je treba opisati tudi vedenje in ne samo podpis metode? Zakaj za rezultat celotnega postopka en `bool` ni dovolj poveden?

## 3. Izdelaj preprost katalog — 15 točk

*Predavanje: poglavje 5. Okvirno 20 minut.*

Sami napišite `KatalogVSpominu : IKatalogKnjig`. Razred naj vsebuje zasebno polje `readonly` tipa `Knjiga[]` in konstruktor, ki prejme polje knjig ter shrani njegovo referenco. Če konstruktor prejme `null`, naj sproži `ArgumentNullException`.

Metodo `Poisci(string naslov)` napišite z zanko `foreach`. Pri ujemanju naslova vrnite obstoječi objekt iz polja. Če ujemanja ni, vrnite `null`. Za zdaj uporabite primerjavo z `==`. Iskanje naj ne ustvarja novih knjig, izposoja knjige ali izpisuje besedila.

V `Program.cs` pripravite polje z obema knjigama in ustvarite katalog. Preverite:

- iskanje obeh znanih naslovov;
- iskanje naslova `Neznana knjiga`;
- iskanje v ločenem katalogu s praznim poljem `new Knjiga[0]`;
- dva zaporedna klica za isti naslov: `object.ReferenceEquals` naj potrdi, da sta rezultata isti objekt;
- stanje najdene knjige po samem iskanju ostane neizposojeno.

Za ta primer so naslovi enolični in polje ne vsebuje elementov `null`. Preverjanja teh dodatnih predpostavk ni treba programirati.

**Na zagovoru:** sledite enemu prehodu zanke in primeru brez zadetka. Zakaj vračamo shranjeni objekt? Kaj bi šlo narobe, če bi vračali `new Knjiga(...)`? Ali `readonly` prepreči spreminjanje stanja knjig?

## 4. Poveži iskanje in izposojo — 15 točk

*Predavanje: poglavji 6 in 7. Okvirno 20 minut.*

Napišite razred `StoritevIzposoje`. V konstruktorju naj prejme `IKatalogKnjig`, preveri, da ni `null`, in referenco shrani v zasebno polje `readonly`.

Napišite metodo:

```csharp
public RezultatIzposoje Izposodi(string? naslov)
```

Telo sestavite sami po teh korakih:

1. Če naslov manjka, je prazen ali vsebuje samo presledke, vrnite `NeveljavenNaslov`.
2. S katalogom poiščite knjigo. Če je rezultat `null`, vrnite `KnjigaNeObstaja`.
3. Na najdeni knjigi pokličite njeno metodo `Izposodi()`. Če vrne `false`, vrnite `ZeIzposojena`.
4. Ob uspehu vrnite `Uspela`.

Uporabite `string.IsNullOrWhiteSpace`. Storitev naj ne vsebuje konzolnega izpisa, lastnega seznama knjig ali ustvarjanja konkretnega kataloga. Stanja knjige naj ne spreminja neposredno.

Za prvi preizkus storitev povežite s katalogom, ki vsebuje nove proste knjige. Izvedite uspešno izposojo in takojšen ponovni poskus na isti knjigi. Preverite vrnjena rezultata in stanje knjige.

**Na zagovoru:** sledite postopku za uspešen, neznan in neveljaven naslov. Kje se posamezna pot konča? Kdo odloči o spremembi stanja? Zakaj konstruktor prejme vmesnik in ne ustvari svojega kataloga?

## 5. Sestavi celoto in prikaži rezultate — 15 točk

*Predavanje: poglavja 7, 8 in 10. Okvirno 15 minut.*

V `Program.cs` na enem mestu ustvarite nove knjige, katalog in storitev. Nato uporabite isto povezano celoto za naslednje zaporedje:

| Korak | Naslov za izposojo     | Pričakovani rezultat |
| ----- | ---------------------- | -------------------- |
| 1     | `Učimo se C#`          | `Uspela`             |
| 2     | `Učimo se C#`          | `ZeIzposojena`       |
| 3     | `Neznana knjiga`       | `KnjigaNeObstaja`    |
| 4     | `""` — prazno besedilo | `NeveljavenNaslov`   |
| 5     | `"   "` — presledki    | `NeveljavenNaslov`   |
| 6     | `null`                 | `NeveljavenNaslov`   |
| 7     | `Osnove algoritmov`    | `Uspela`             |

Za vsak rezultat s `switch` pripravite razumljivo sporočilo. Besedilo lahko izberete sami, vendar naj jasno razlikuje vse štiri izide. `switch` in izpis sodita v prikaz, ne v katalog ali storitev.

V prikazu ne ponavljajte iskanja ali poskusa izposoje, da bi ugotovili, kaj napisati. Uporabite rezultat že izvedenega klica. Pred drugim korakom ne ustvarite nove storitve z novimi knjigami.

Zapišite pričakovane in dejanske rezultate. Skicirajte pot klica `Program → StoritevIzposoje → katalog → Knjiga` in pot rezultata nazaj. Označite, kje se pot prekine pri neobstoječi knjigi.

**Na zagovoru:** pokažite mesto sestave objektov ter sledite drugemu poskusu izposoje. Kaj bi se zgodilo, če bi pred vsakim klicem ustvarili nove knjige? Kako se odvisnost storitve od vmesnika razlikuje od konkretnega objekta, ki med zagonom izvede iskanje?

## 6. Zamenjaj izvedbo kataloga — 10 točk

*Predavanje: poglavji 9 in 12. Okvirno 10 minut.*

Napišite razred `PrazenKatalog : IKatalogKnjig`. Njegova metoda `Poisci` naj vedno vrne `null`.

Ustvarite novo storitev in ji podajte `PrazenKatalog`. Kode razreda `StoritevIzposoje` ne spreminjajte. Preverite:

- za `Učimo se C#` dobite `KnjigaNeObstaja`;
- za prazen naslov dobite `NeveljavenNaslov`.

Za primerjavo ohranite prikaz storitve s katalogom iz naloge 5. Obe izvedbi morata ustrezati istemu vmesniku.

**Na zagovoru:** pojasnite, kaj ste zamenjali in zakaj se storitev ni spremenila. Kaj pomeni majhna sklopljenost na tem primeru? Ali vmesnik sam zagotovi, da metoda ne spreminja stanja knjige? Zakaj prazen katalog ni isto kot napaka pri povezovanju s podatkovno zbirko?

## 7. Poišči napako v sodelovanju modulov — 15 točk

*Predavanje: poglavji 10 in 11. Okvirno 20 minut.*

V `KatalogVSpominu.Poisci` začasno zamenjajte pravilni stavek:

```csharp
return knjiga;
```

z napačnim:

```csharp
return new Knjiga(knjiga.Naslov, knjiga.Avtor);
```

Pred spremembo poskrbite, da lahko obnovite pravilni stavek. Poskus začnite z novim poljem prostih knjig in novo povezano storitvijo. Dvakrat zapored zahtevajte izposojo istega naslova.

Zapišite:

1. Kaj ste pričakovali in kaj program dejansko vrne?
2. Ali se stanje knjige, shranjene v polju, spremeni?
3. Kateri modul krši dogovor in zakaj?
4. Zakaj preizkus `Knjiga.Izposodi()` na enem samem objektu te napake ne bi odkril?

V razhroščevalniku postavite prekinitveno točko po iskanju v storitvi in pred spremembo stanja v knjigi. Opazujte najdeni objekt, njegovo stanje in klicni sklad. Za dokaz različnih referenc lahko uporabite tudi `object.ReferenceEquals`.

Obnovite pravilni `return knjiga;`. Ponovite zaporedje iz naloge 5 in preverjanji iz naloge 6. Končna oddaja naj vsebuje popravljeno različico; v kratkem zapisu ohranite opis vzroka in popravka.

**Na zagovoru:** napako pojasnite s konkretnima objektoma, ne samo z besedami »napačna referenca«. Razlikujte preverjanje posameznega dela, sodelovanja delov in celotne poti do prikaza. Povejte, čemu služi ponovitev že uspešnih preverjanj po popravku.

## 8. Majhna izboljšava in majhna sprememba dogovora — 10 točk

*Predavanje: poglavji 12 in 13. Okvirno 15 minut.*

**A. Uredi prikaz brez spremembe vedenja.** Preslikavo rezultata v sporočilo iz naloge 5 prestavite v eno lokalno funkcijo v `Program.cs`:

```csharp
static string OblikujSporocilo(RezultatIzposoje rezultat)
```

Funkcija naj uporablja `switch` in vrne besedilo; izpis ostane pri klicatelju. Vsi primeri naj uporabljajo to funkcijo. Ohranite ista sporočila in preverite vse štiri rezultate. Lokalna funkcija znotraj vrhnjega programa zadostuje; nov razred ni potreben.

**B. Spremeni pravilo iskanja.** Dogovor spremenite tako, da velikost črk pri iskanju ni pomembna. Za ta tehnični primer uporabite:

```csharp
string.Equals(knjiga.Naslov, naslov, StringComparison.OrdinalIgnoreCase)
```

S tem zamenjajte primerjavo v katalogu in posodobite komentar ob vmesniku. Ta vaja ne zahteva jezikovnega iskanja, odstranjevanja diakritičnih znamenj ali odstranjevanja presledkov okoli naslova.

V ločenem poskusu uporabite nov katalog s prostimi knjigami. Najprej zahtevajte `učimo se c#`, nato `UČIMO SE C#`. Pričakovana rezultata sta `Uspela` in `ZeIzposojena`, ker oba naslova pomenita isti shranjeni objekt. Ponovite še preverjanja iz nalog 5 in 6; vsako zaporedje začnite v njegovem opisanem začetnem stanju.

V dveh stavkih pojasnite razliko: kateri poseg je refaktoriranje in kateri spremeni vedenje? Ali se mora zaradi novega dogovora spremeniti tudi `PrazenKatalog`?

**Na zagovoru:** pokažite, zakaj sprememba prikaza ne zahteva spremembe storitve. Pojasnite, katere teste in dokumentacijo je treba prilagoditi ob spremembi iskanja ter kako dobre meje zmanjšajo obseg popravkov.

## Oddaja in zagovor

Oddajte projekt z izvornimi datotekami ter kratek dokument `odgovori.md` ali drug berljiv zapis. V njem naj bodo odgovornosti, skica poti klica, preglednica preverjanj in kratke razlage iz nalog. Zadostujejo kratki odgovori, brez obsežnega poročila. Map `bin` in `obj` ni treba oddati.

Končni projekt naj se prevede in prikaže popravljeno delovanje ter izboljšano iskanje iz naloge 8. Posamezne prikaze v `Program.cs` ločite s komentarji ali oznakami izpisa. Za neodvisna zaporedja ustvarite nove začetne podatke; za ponovljeno izposojo znotraj zaporedja uporabite isti objekt.

Na zagovoru boste ob svoji kodi sledili enemu uspešnemu in enemu neuspešnemu klicu. Pokazati morate, kdo išče, kdo spreminja stanje in kdo prikaže rezultat. Izvajalec lahko zahteva majhno spremembo, na primer dodajanje knjige v začetno polje ali spremembo enega sporočila. Celotnega sistema na novo ne pišete.
