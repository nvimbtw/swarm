# Vaje 4: razredi, objekti in sodelovanje v majhnih korakih

V jeziku C# ustvarite konzolni projekt `Vaja004`. Pomagajte si s [predavanjem četrtega tedna](004-predavanje.md). Skupaj je mogoče doseči **100 točk: 70 za izdelavo in preverjanje ter 30 za razumevanje na zagovoru**. Podrobnosti so v [točkovniku](004-vaje-tockovnik.md).

Glavni primer je knjiga, ki si jo lahko izposodimo in jo vrnemo. Razred napišite sami in ga postopoma dopolnjujte. Pri vsaki nalogi najprej napovejte rezultat, nato program zaženite in preverite napoved. Pomembno je, da znate pokazati, kaj se v vašem programu zgodi in zakaj.

Zadostujejo vnaprej zapisani podatki in izpisi v konzoli. Meniji, vnos s tipkovnice, podatkovna zbirka, spletna aplikacija in dejanska povezava na internet niso del teh vaj. Za naprednejše teme so pripravljena kratka vprašanja in izseki za razumevanje.

Naloge 1–6 nadgrajujejo isti primer. Naloga 7 je razmislek, naloga 8 je majhen ločen primer v istem projektu, naloga 9 je branje izsekov, naloga 10 pa uredi preverjanja, ki jih izvajate sproti. Okvirno je predvidenih 120 minut dela in 15 minut skupne razlage; individualni zagovor se izvede po dogovoru z izvajalcem.

## 1. Moj prvi razred in dva objekta — 10 točk

*Predavanje: poglavja 1–4 in 9. Okvirno 10 minut.*

Pred pisanjem kode v treh kratkih vrsticah zapišite: kaj bo predstavljal razred `Knjiga`, katere podatke potrebuje in kateri dejanji bo pozneje omogočal. Izberite še en podatek o fizični knjigi, ki ga za ta primer ne potrebujemo, in pojasnite izbiro.

V datoteki `Knjiga.cs` sami napišite javni razred `Knjiga` s temi lastnostmi:

| Lastnost       | Tip      | Dostop                                    |
| -------------- | -------- | ----------------------------------------- |
| `Naslov`       | `string` | Javno branje, nastavitev v konstruktorju. |
| `Avtor`        | `string` | Javno branje, nastavitev v konstruktorju. |
| `JeIzposojena` | `bool`   | Javno branje, zasebno nastavljanje.       |

Napišite konstruktor `Knjiga(string naslov, string avtor)`. Shrani naj naslov in avtorja ter določi začetno stanje: knjiga ni izposojena. Preverjanje praznega besedila boste dodali pri nalogi 3.

V `Program.cs` ustvarite dve knjigi z različnima naslovoma in izpišite njune podatke ter začetno stanje. Uporabite poljubna naslova in avtorja.

**Preverite:** ustvarili ste dva objekta istega razreda; obe knjigi imata začetno stanje `false`.

**Na zagovoru:** pokažite razred, oba objekta, lastnost, parameter in argument. Pojasnite `new` in konstruktor ter zakaj za dva naslova ne potrebujemo dveh razredov. Na svojem načrtu pojasnite, kaj pomeni izbrati samo podatke, pomembne za namen programa.

## 2. Metodi spreminjata stanje — 15 točk

*Predavanje: poglavja 3, 4 in 6. Okvirno 15 minut.*

V razred `Knjiga` dodajte metodi:

```csharp
public bool Izposodi()
public bool Vrni()
```

To sta zahtevana podpisa; telo vsake metode napišite sami.

`Izposodi()` naj pri prosti knjigi spremeni stanje v izposojeno in vrne `true`. Če je knjiga že izposojena, naj vrne `false` in stanje pusti nespremenjeno.

`Vrni()` naj pri izposojeni knjigi spremeni stanje v prosto in vrne `true`. Če knjiga ni izposojena, naj vrne `false` in stanje pusti nespremenjeno.

Metodi naj rezultat vrneta. Izpis rezultata in stanja napišite v `Program.cs`.

Na novi knjigi izvedite to zaporedje:

| Korak | Klic         | Pričakovani rezultat | `JeIzposojena` po klicu |
| ----- | ------------ | -------------------- | ----------------------- |
| 1     | `Izposodi()` | `true`               | `true`                  |
| 2     | `Izposodi()` | `false`              | `true`                  |
| 3     | `Vrni()`     | `true`               | `false`                 |
| 4     | `Vrni()`     | `false`              | `false`                 |

**Na zagovoru:** po korakih sledite eni metodi. Pojasnite, kaj pomeni vrnjeni `bool`, kdaj se izvede `return` in kako se vračanje rezultata razlikuje od izpisa. Povejte, katero pravilo varujeta metodi.

## 3. Lastnost in zaščita veljavnega stanja — 10 točk

*Predavanje: poglavja 3, 5 in 6. Okvirno 15 minut.*

Dodajte izračunano lastnost `JeNaVoljo` tipa `bool`. Ima naj samo `get` in vrača nasprotno vrednost `JeIzposojena`. Zanjo ne uvajajte dodatnega polja ali nastavljalnika.

Konstruktor dopolnite tako, da za prazen naslov ali avtorja oziroma besedilo iz samih presledkov sproži `ArgumentException`. Pomagate si lahko z `string.IsNullOrWhiteSpace` in zgledom konstruktorja v predavanju.

Začasno poskusite v `Program.cs` zapisati:

```csharp
knjiga.JeIzposojena = true;
```

Uporabite ime svoje spremenljivke. Preberite sporočilo prevajalnika, zapišite njegov pomen in nedovoljeni stavek nato zakomentirajte. Končna oddaja naj se prevede.

**Preverite:** `JeNaVoljo` pred izposojo in po njej; ločeno poskusite ustvariti knjigo s praznim naslovom ter knjigo z avtorjem iz samih presledkov. Izjemo lahko opazujete v razhroščevalniku in nato poskus zakomentirate. Lastnega sistema obravnave izjem ni treba izdelati.

**Na zagovoru:** pojasnite `get`, `private set` in razliko med poljem ter lastnostjo. Zakaj je razpoložljivost izračunana? Kaj prepreči konstruktor? Ali `private` pomeni tudi prijavo uporabnika oziroma preverjanje njegovih pravic?

## 4. Dve referenci ali dva objekta? — 10 točk

*Predavanje: poglavje 7. Okvirno 10 minut.*

Pred zagonom napovejte izpis vsake vrstice. Nato izsek zaženite s svojim razredom `Knjiga`:

```csharp
Knjiga prva = new Knjiga("Učimo se C#", "Ana Novak");
Knjiga druga = prva;
Knjiga tretja = new Knjiga("Učimo se C#", "Ana Novak");

druga.Izposodi();

Console.WriteLine(prva.JeIzposojena);
Console.WriteLine(tretja.JeIzposojena);
Console.WriteLine(object.ReferenceEquals(prva, druga));
Console.WriteLine(object.ReferenceEquals(prva, tretja));
```

Narišite spremenljivke in puščice do objektov. Zadostuje skica na papirju ali z besedilom.

Izseku dodajte `druga = tretja;`. Nato ponovno izpišite stanje `prva` in preverite, ali se je zaradi te prireditve spremenilo.

Za naslednji izsek zadostuje razlaga brez zagona:

```csharp
Knjiga? manjka = null;
// manjka.Izposodi();
```

**Na zagovoru:** povejte, koliko objektov je nastalo, kaj se prekopira pri prireditvi in zakaj enaka naslova ne pomenita istega objekta. Pojasnite `null`, oznako `?` in posledico klica metode, če reference ne preverimo.

## 5. Instančna metoda, statična metoda in ponovna uporaba — 10 točk

*Predavanje: poglavja 5, 8, 11 in 12. Okvirno 10 minut.*

V novi datoteki ustvarite `public static class OpisKnjige`. Vanj napišite metodo:

```csharp
public static string Oblikuj(string naslov, string avtor)
```

Metoda naj vrne besedilo v obliki `Naslov — Avtor`. Znotraj metode rezultat najprej shranite v lokalno spremenljivko `opis` in jo vrnite. Metoda naj ne izpisuje v konzolo in naj ne hrani skupnega spremenljivega stanja.

V `Program.cs` pokličite isto metodo za obe knjigi iz naloge 1 in izpišite rezultata. Razred `Knjiga` naj ostane v svoji datoteki, prikaz pa v `Program.cs`.

**Na zagovoru:** primerjajte `knjiga.Izposodi()` in `OpisKnjige.Oblikuj(...)`. Zakaj je stanje izposoje instančno? Kje obstaja lokalni `opis`? Zakaj bi lahko isti razred `Knjiga` in isto oblikovanje uporabili tudi v aplikaciji z okni? Pojasnite, zakaj razdelitev datotek sama še ne zagotavlja smiselnih odgovornosti.

## 6. En objekt uporablja drugega — 10 točk

*Predavanje: poglavja 9–11 in 13. Okvirno 15 minut.*

Napišite razred `BralnikKnjige`. V tej vaji predstavlja preprost objekt, ki zna pripraviti opis ene knjige. Potrebuje:

- zasebno polje `private readonly Knjiga knjiga`;
- konstruktor `BralnikKnjige(Knjiga knjiga)`, ki prejeto referenco shrani v polje in pri `null` sproži `ArgumentNullException`;
- metodo `public string PripraviOpis()`, ki vrne opis shranjene knjige s klicem `OpisKnjige.Oblikuj(...)` iz naloge 5.

V konstruktorju uporabite `this.knjiga = knjiga;` in pojasnite obe strani prireditve. Znotraj razreda ne ustvarjajte nove knjige in ne kopirajte njenih podatkov v dodatna polja.

V `Program.cs` ustvarite knjigo, jo podajte konstruktorju bralnika in izpišite rezultat `PripraviOpis()`. Skicirajte povezavo `Program → BralnikKnjige → Knjiga` ter označite še klic oblikovanja opisa.

**Preverite:** rezultat vsebuje podatke podane knjige. Za drugo knjigo ustvarite drug bralnik; kode razreda zaradi drugega naslova ne spreminjajte.

**Na zagovoru:** pokažite polje, lastnost knjige in lokalno spremenljivko iz naloge 5. Pojasnite `this`, `readonly` in podajanje odvisnosti prek konstruktorja. Ali `readonly` prepreči klic `Izposodi()` na shranjeni knjigi? Ali bralnik »je knjiga« ali knjigo »uporablja«? Ali knjiga preneha obstajati samo zato, ker je bralnik ne uporablja več?

## 7. Dedovanje in odgovornosti: kratek razmislek — 5 točk

*Predavanje: poglavja 6 in 9–12. Okvirno 5 minut.*

Pri tej nalogi ne pišete novega razreda. V nekaj stavkih oziroma s preprosto skico odgovorite:

1. Če bi model razširili, bi bila `ElektronskaKnjiga` lahko posebna vrsta `Knjiga`. Kaj bi moral tak model še vedno omogočati, da bi jo lahko uporabljali namesto osnovne knjige? Ali je dedovanje potrebno samo zato, ker imata dva tipa naslov?
2. Zakaj `BralnikKnjige` ne bi dedoval od `Knjiga`? Kako ste pri nalogi 6 ponovno uporabili kodo brez dedovanja?
3. V katerem delu bi spremenili obliko izpisa in v katerem pravilo izposoje? Zakaj teh odgovornosti ne združimo v eno veliko metodo?
4. Če bi dodali seznam aktivnih izposoj, na kaj bi morali paziti ob obstoječi lastnosti `JeIzposojena`? Ali trenutni razred sam zagotovi enoličnost knjig v celotni evidenci in uskladi dva ločena programa?

**Na zagovoru:** s svojimi besedami ločite »je vrsta« od »uporablja«. Na primeru odgovornosti pojasnite tudi povezanost znotraj modula in sklopljenost med moduli. Zadostuje smiselna razlaga, ne naučena definicija. Za `protected` in `internal` morate prepoznati osnovni pomen iz predavanja; v svoji kodi ju ni treba uporabiti. Enako velja za `virtual` in `override`: vedite, da omogočata prepisovanje vedenja pri dedovanju, izvedbe pa ne zahtevamo.

## 8. En vmesnik, dve preprosti izvedbi — 15 točk

*Predavanje: poglavja 12 in 13. Okvirno 20 minut.*

Pripravite majhen vremenski primer. Za zdaj uporablja navadno vrnjeno število in deluje brez interneta.

Napišite vmesnik:

```csharp
public interface IVirTemperature
{
    double PridobiTemperaturo();
}
```

Nato sami napišite dva razreda, ki ga implementirata:

- `TopelVir`: metoda vrne `18.0`;
- `HladenVir`: metoda vrne `-5.0`.

Dogovor je, da rezultat pomeni temperaturo v stopinjah Celzija. Razreda imata lahko vsak samo eno kratko metodo.

Napišite še razred `Merilnik`. Po vzoru naloge 6 naj prek konstruktorja prejme `IVirTemperature`, referenco shrani v zasebno polje `readonly` in pri `null` sproži `ArgumentNullException`. Metoda `public double Preberi()` naj pokliče shranjeni vir in vrne njegov rezultat. Ne dodajajte pogojev za ugotavljanje, ali je vir topel ali hladen.

V `Program.cs` uporabite oba vira, na primer s temi klici:

```csharp
Merilnik prviMerilnik = new Merilnik(new TopelVir());
Merilnik drugiMerilnik = new Merilnik(new HladenVir());

Console.WriteLine(prviMerilnik.Preberi());
Console.WriteLine(drugiMerilnik.Preberi());
```

**Preverite:** prvi rezultat je 18, drugi −5; koda razreda `Merilnik` je za oba primera ista.

**Na zagovoru:** pokažite vmesnik in njegovi izvedbi. Pojasnite, zakaj ne napišemo `new IVirTemperature()`, kako merilnik uporablja vir in kje se pokaže polimorfizem. Kaj bi morali dodati za tretji vir in ali bi morali spremeniti `Merilnik`? Kako se ta programski vmesnik razlikuje od uporabniškega vmesnika?

## 9. Priprava na zunanji sistem: preberi in pojasni — 10 točk

*Predavanje: poglavji 13 in 14. Okvirno 10 minut.*

Pri tej nalogi ne izdelujete omrežne povezave. Odgovore zapišite kratko, lahko po alinejah.

**A. Pot podatkov.** Narišite zaporedje: uporabnik zahteva podatek, aplikacija pokliče spletni vir, spletni vir pošlje zahtevo HTTP, zunanja storitev vrne odgovor, aplikacija prikaže rezultat. Označite mejo med našim programom in zunanjim sistemom. Kateri novi razred bi lahko dodali namesto učnega vira? Kje bi bila odgovornost za naslov storitve in pretvorbo odgovora? Kaj pomeni spletni API in za kaj bi uporabili `HttpClient`?

**B. Podatki in napake.** Spodnji JSON je izmišljen učni odgovor:

```json
{
  "location": "Maribor",
  "temperature": 18.0,
  "unit": "C"
}
```

Pojasnite serializacijo in deserializacijo. Navedite dve vsebinski preverjanji po branju odgovora. Kaj bi prikazali, če bi bil strežnik nedosegljiv ali bi odgovor prispel prepozno? Zakaj ne vrnemo preprosto 0? Ali sme biti skrivni ključ zapisan v izvorni kodi, ki jo javno delimo?

**C. Kratek asinhroni izsek.** Naslednji izsek obravnavajte ločeno od naloge 8. Preberite ga; zagon in predelava svojega projekta nista zahtevana.

```csharp
Task<double> opravilo = Task.FromResult(18.0);
double temperatura = await opravilo;
Console.WriteLine(temperatura);
```

Kaj predstavlja `Task<double>`, kaj v tem primeru naredi `Task.FromResult` in kaj dobimo po `await`? Ali ta izsek že dostopa do interneta? Ali `await` sam po sebi ustvari novo nit? Kako bi se vrnjeni tip metode iz naloge 8 spremenil, če bi dogovor omogočal asinhrono pridobivanje?

**Na zagovoru:** po svoji skici sledite podatku od zahteve do prikaza. Razlikujte lokalni klic metode in spletno zahtevo ter uspeh in neuspeh pridobivanja. Zadostuje osnovno razumevanje izseka; podrobnosti upravljanja niti niso zahtevane.

## 10. Moja kratka preverjanja — 5 točk

*Predavanje: poglavje 15. Okvirno 10 minut za ureditev sprotnih zapisov.*

Pripravite kratko preglednico s stolpci: **začetno stanje oziroma vhod, dejanje, pričakovani rezultat, dejanski rezultat**. Uporabite preverjanja, ki ste jih že opravili:

1. Začetno stanje nove knjige.
2. Prva in ponovljena izposoja iste knjige.
3. Vrnitev izposojene knjige in ponovljena vrnitev.
4. Zavrnitev praznega naslova in avtorja iz samih presledkov.
5. Sprememba prek druge reference na isti objekt ter nespremenjeno stanje ločenega objekta.
6. Bralnikov opis podane knjige.
7. Rezultata obeh temperaturnih virov.

Zaporedja lahko zapišete v več vrsticah. Pri vsakem primeru mora biti jasno, ali uporabite nov objekt ali nadaljujete prejšnje zaporedje. Avtomatiziranih testov in dodatnih knjižnic ni treba pisati.

**Na zagovoru:** izberite en primer in povejte, katero napako bi lahko odkril. Pojasnite, zakaj uspešna uporaba učnega temperaturnega vira še ne potrdi povezave z resnično vremensko storitvijo in kaj bi morali pri njej preveriti ločeno.

## Kaj oddate in kako poteka zagovor

Oddajte konzolni projekt z datoteko projekta in izvornimi datotekami ter kratek zapis napovedi, odgovorov in preverjanj. Zapis je lahko v `odgovori.md` ali drugem berljivem dokumentu. Zadostujejo kratki odgovori; daljše poročilo ni potrebno. Map `bin` in `obj` ni treba oddati.

Končna različica naj se prevede. Namerno napačne vrstice iz nalog 3 in 4 naj ostanejo zakomentirane. Poskuse z izjemami lahko prav tako pustite zakomentirane, da se običajni prikaz drugih nalog nemoteno izvede. Posamezne prikaze v `Program.cs` jasno ločite s komentarji ali kratkimi oznakami izpisa.

Na zagovoru boste ob svoji kodi pojasnili osnovne pojme in napovedali rezultat kratkega zaporedja. Izvajalec lahko zahteva majhno spremembo, na primer ustvarjanje še enega objekta, dodaten klic metode ali spremembo vrednosti učnega vira. Vsa vprašanja se navezujejo na zgornje naloge; dodatnega sistema ali zahtevnejšega algoritma ni treba izdelati.
