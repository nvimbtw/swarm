# Vaje 6: podatkovna zbirka in branje iz aplikacije

*11. 11. 2026 · Podatkovni vmesniki in zbirke*

Pripravite majhno zbirko SQLite z avtorji in knjigami ter konzolni projekt `Vaja006` v C#. Po korakih boste napisali SQL, preverili rezultate in podatke prebrali prek svojega razreda. Pomagajte si s [predavanjem](006-predavanje.md).

**Cilj je razumeti, od kod podatek pride in kako ga aplikacija prebere.** Za vsak primer najprej napovejte rezultat, nato ga preverite. Razlikovati morate zapis v zbirki, objekt v pomnilniku in prikaz podatka.

Zadostujejo dve tabeli, en pogled, nekaj kratkih poizvedb in preprost program za branje. Urejanja prek obrazcev, izposojanja, podatkovnega strežnika in spletne povezave ne izdelujete. Predznanje objektov iz prejšnjih tednov uporabite pri podatkovnem razredu in razredu za dostop.

Skupaj je mogoče doseči **100 točk: 40 za izdelavo in preverjanje ter 60 za razumevanje na zagovoru**. Merila so v [točkovniku](006-vaje-tockovnik.md). Okvirno je predvidenih 120 minut dela in 15 minut skupne razlage; osnovno razvojno okolje in dostop do paketov naj bosta pripravljena pred vajami. Zagovor poteka po dogovoru z izvajalcem.

## 1. Načrt dveh tabel in priprava projekta — 10 točk

*Predavanje: poglavja 1–4. Okvirno 10 minut.*

V našem učnem modelu ima knjiga natanko enega avtorja, avtor pa lahko nima knjig ali jih ima več. Narišite tabeli in povezavo:

- `Avtor`: `Id`, `Ime`;
- `Knjiga`: `Id`, `Naslov`, `Leto`, `AvtorId`.

Označite primarna ključa in tuji ključ. Zapišite, zakaj ne prepisujemo imena avtorja v vsako knjigo in zakaj identifikatorja ne nadomestimo z naslovom. Gre za knjige v preprostem katalogu, ne za evidenco posameznih izposojenih izvodov.

Ustvarite projekt in dodajte ponudnika. Ukaze izvedite v terminalu:

```powershell
dotnet new console -n Vaja006
Set-Location Vaja006
dotnet add package Microsoft.Data.Sqlite
```

Uporabite .NET SDK, ki ga uporabljate pri predmetu, in stabilno različico paketa, združljivo s projektom. Namestitev paketa potrebuje dostop do vira NuGet; poznejši zagon z obnovljenimi paketi in lokalno zbirko ne potrebuje zunanje podatkovne storitve. Če dostopa do paketov ni, naj okolje pripravi izvajalec.

V `Program.cs` prenesite začetno kodo iz dodatka na koncu teh navodil. Dovoljeno jo je neposredno uporabiti. Omogoča pripravo zbirke, izvajanje vaših datotek SQL in poznejše dodajanje kode za branje. Vse nadaljnje ukaze izvajajte iz mape, ki vsebuje `Vaja006.csproj`.

**Na zagovoru:** pokažite tabeli, ključe in povezavo. Razlikujte SQL, SQLite in paket za C#. Zakaj je baza lahko lokalna datoteka in vseeno uporabljamo objekt povezave?

## 2. Ustvari zbirko in vstavi začetne podatke — 15 točk

*Predavanje: poglavje 5. Okvirno 20 minut.*

Sami napišite datoteko `priprava.sql`. Uporabite `CREATE TABLE`, `INTEGER`, `TEXT`, `PRIMARY KEY`, `NOT NULL`, `FOREIGN KEY` in `INSERT INTO`. Pomagate si lahko z zgledom iz predavanja, vendar morate znati pojasniti posamezne dele.

Vsi navedeni stolpci so obvezni. `Knjiga.AvtorId` se mora sklicevati na `Avtor.Id`. Na začetek skripta dodajte `PRAGMA foreign_keys = ON;`. Vnesite točno te izmišljene podatke:

| Avtor.Id | Ime |
| ---: | --- |
| 1 | Ana Novak |
| 2 | Miha Kranjc |
| 3 | Eva Kos |

| Knjiga.Id | Naslov | Leto | AvtorId |
| ---: | --- | ---: | ---: |
| 1 | Učimo se C# | 2024 | 1 |
| 2 | Osnove algoritmov | 2023 | 2 |
| 3 | Programiranje po korakih | 2025 | 1 |

Avtorje vstavite pred knjigami. Za novo zbirko izvedite:

```powershell
dotnet run -- --pripravi
```

Začetna koda ustvari `knjiznica.db` v trenutni mapi. Ukaz uporabite enkrat za novo zbirko; običajni zagon je samo `dotnet run`. Skript ne briše že obstoječih tabel. Če priprava delno odpove in želite začeti znova, ustavite program ter svojo učno datoteko `knjiznica.db` preimenujte, da se ob naslednji pripravi ustvari nova. Obstoječih zbirk iz drugih nalog ne spreminjajte.

Za začetni pregled ustvarite `pregled.sql` z ukazi:

```sql
SELECT Id, Ime FROM Avtor ORDER BY Id;
SELECT Id, Naslov, Leto, AvtorId FROM Knjiga ORDER BY Id;
PRAGMA foreign_keys;
```

Zaženite `dotnet run -- --sql pregled.sql`. Pričakujete tri avtorje, tri knjige in vrednost 1 za vključeno preverjanje tujih ključev. Ponovni pregled po koncu programa mora vrniti iste shranjene podatke.

**Na zagovoru:** pojasnite vrstni red vnosa, primarni in tuji ključ ter `NOT NULL`. Ali slednji prepove tudi prazno besedilo? Kaj ostane shranjeno po zaprtju programa?

## 3. Napiši in preveri osnovne poizvedbe — 15 točk

*Predavanje: poglavje 6. Okvirno 15 minut.*

V datoteko `poizvedbe.sql` sami napišite tri poizvedbe:

1. Izpiši `Id`, `Naslov` in `Leto` vseh knjig, urejenih po `Id`.
2. Izpiši iste stolpce za knjige od leta 2024 naprej, urejene po `Leto`, nato `Id`.
3. Vrni število vseh knjig z imenom rezultata `SteviloKnjig`.

Zaženite `dotnet run -- --sql poizvedbe.sql`. Pričakujete najprej knjige 1, 2 in 3; nato knjigi 1 in 3; pri štetju vrednost 3. Poizvedbo s pogojem preizkusite še z letom 2030: rezultat nima nobene podatkovne vrstice. Končni zapis v datoteki naj vsebuje obe preverjeni meji, jasno ločeni s komentarjem SQL `--`.

**Na zagovoru:** pojasnite `SELECT`, `FROM`, `WHERE`, `ORDER BY` in `COUNT(*)`. Zakaj ne predpostavimo vrstnega reda brez `ORDER BY`? Kako se prazni rezultat razlikuje od rezultata štetja z vrednostjo 0? Kako bi preverjali manjkajočo vrednost `NULL`?

## 4. Poveži tabeli in pripravi pogled — 10 točk

*Predavanje: poglavji 7 in 8. Okvirno 15 minut.*

V `povezava.sql` napišite poizvedbo z `INNER JOIN`, ki vrne `Id` knjige, `Naslov`, ime avtorja pod imenom `Avtor` ter `Leto`. Povežite `Knjiga.AvtorId` z `Avtor.Id` in rezultat uredite po identifikatorju knjige.

Pričakujete:

| Id | Naslov | Avtor | Leto |
| ---: | --- | --- | ---: |
| 1 | Učimo se C# | Ana Novak | 2024 |
| 2 | Osnove algoritmov | Miha Kranjc | 2023 |
| 3 | Programiranje po korakih | Ana Novak | 2025 |

V ločeni datoteki `pogled.sql` ustvarite pogled `PregledKnjig` z istimi štirimi stolpci. Definicija pogleda naj vsebuje povezavo tabel; razvrščanje določite pri poizvedbi, ki pogled bere. Datoteko za ustvarjanje pogleda izvedite enkrat z `dotnet run -- --sql pogled.sql`.

V `povezava.sql` dodajte še branje iz pogleda in primerjajte oba rezultata. Po obnovi zbirke iz naloge 2 je treba ponovno ustvariti tudi pogled.

Preberite zgled `LEFT JOIN` v predavanju in napovejte, katero dodatno vrstico bi dobili za Evo Kos. Tega dodatnega stika ni treba samostojno programirati.

**Na zagovoru:** pokažite pogoj povezave. Zakaj je Ana v rezultatu dvakrat, Eva pa ni? Kako se razlikujeta tabela in pogled? Ali pogled hrani neodvisno kopijo vseh prikazanih vrstic?

## 5. Preberi vrstice prek odprte povezave — 15 točk

*Predavanje: poglavja 9–11. Okvirno 15 minut.*

V označeni del začetnega `Program.cs` dodajte svojo kodo za običajen zagon brez argumentov. Uporabite že pripravljeni `povezovalniNiz`, ki za običajni zagon določa `ReadOnly`.

Sami napišite naslednje korake:

1. Ustvarite `SqliteConnection` in jo odprite z `Open()`.
2. Ustvarite ukaz in določite `SELECT Id, Naslov FROM Knjiga ORDER BY Id;`.
3. Izvedite `ExecuteReader()`.
4. V zanki `while (reader.Read())` preberite `Id` z `GetInt32(0)` in naslov z `GetString(1)` ter ju izpišite.
5. Povezavo, ukaz in bralnik sprostite z `using`.

Da je območje življenjske dobe jasno, kodo te naloge lahko obdajte z dodatnima zavitima oklepajema. Za njima boste dodali prikaze naslednjih nalog; bralnik in povezava te naloge bosta takrat že sproščena.

Zaženite `dotnet run`. Pričakujete tri vrstice. Začasno dodajte pogoj `WHERE Leto >= 2030` pred `ORDER BY` in preverite, da se telo zanke ne izvede. Nato obnovite začetno poizvedbo.

**Na zagovoru:** pokažite povezavo, ukaz in bralnik. Kdaj se pomaknemo na prvo vrstico? Kaj pomenita indeksa 0 in 1? Kaj se zgodi s praznim rezultatom in kdaj se v vašem programu povezava sprosti?

## 6. Napiši majhen razred za dostop do podatkov — 15 točk

*Predavanje: poglavje 12. Okvirno 20 minut.*

Ustvarite razred `KnjigaPodatek` s štirimi javno berljivimi lastnostmi: `int Id`, `string Naslov`, `string Avtor`, `int Leto`. Konstruktor naj prejme in shrani vse štiri vrednosti. Za razliko od starega razreda za izposojo ta razred samo predstavlja prebrano vrstico.

Dodajte vmesnik:

```csharp
public interface IBranjeKnjig
{
    List<KnjigaPodatek> PreberiOdLeta(int najmanjLeto);
}
```

Nato sami sestavite `SqliteBranjeKnjig : IBranjeKnjig`. Konstruktor naj prejme povezovalni niz in ga shrani v zasebno polje `readonly`. Metoda naj:

- ustvari prazen seznam;
- odpre povezavo, ustvari ukaz in prebere pogled `PregledKnjig`;
- izbere `Id`, `Naslov`, `Avtor`, `Leto`, uporabi pogoj `Leto >= $leto` in razvrsti po `Id`;
- vrednost parametra poda z `ukaz.Parameters.AddWithValue("$leto", najmanjLeto)`;
- za vsako vrstico ustvari `KnjigaPodatek` in ga doda na seznam;
- vrne seznam, pri tem pa sprosti bralnik, ukaz in povezavo z `using`.

Razred naj ne izpisuje v konzolo. Prazen rezultat naj vrne kot prazen seznam; napake povezave ne prikrivajte s praznim seznamom. Vrednosti leta ne lepite neposredno v besedilo SQL.

V `Program.cs` objekt uporabite prek `IBranjeKnjig`. Preverite leto 2024, kjer pričakujete knjigi 1 in 3, ter leto 2030, kjer pričakujete seznam z `Count` 0. Prikaz napišite pri klicatelju. Pomagate si lahko s celotnim zgledom v predavanju, vendar morate znati vsak korak pojasniti.

**Na zagovoru:** sledite letu od argumenta metode do parametra SQL in eni vrstici nazaj do objekta. Zakaj je vrstni red stolpcev pomemben? Kaj poznata prikaz in razred za dostop? Ali `$leto` pomeni interpolacijo C#?

## 7. Uporabi podatke po zaprtju povezave — 10 točk

*Predavanje: poglavje 13. Okvirno 15 minut.*

Pokličite `PreberiOdLeta(0)` in rezultat shranite v lokalni seznam. Pri naših podatkih pričakujete tri elemente. Po vrnitvi metode jih izpišite z `foreach`.

Na svoji kodi označite, kje se povezana faza branja konča in kje uporabljate že prebrane podatke brez odprte povezave. To lahko pokažete tudi s prekinitveno točko pri klicatelju po vrnitvi metode.

Nato na lokalnem seznamu pokličite `Clear()` in izpišite njegovo število elementov. Ponovno pokličite `PreberiOdLeta(0)` in izpišite število novega seznama. Pričakujete zaporedje števil 3, 0, 3. V zbirki mora ostati enako število knjig.

V treh kratkih stavkih pojasnite:

1. Zakaj je po koncu metode mogoče uporabljati seznam, ne pa zaprtega bralnika?
2. Zakaj `Clear()` na seznamu ne izbriše vrstic v zbirki?
3. Zakaj nov objekt z istim `Id` tokrat ni enaka napaka kot ustvarjanje nove kopije knjige pri vsaki izposoji v Tednu5?

**Na zagovoru:** razlikujte povezani in nepovezani način uporabe ter omenite svežino podatkov. Vedeti morate tudi, da je `DataTable` lokalna tabela, `DataSet` lahko vsebuje več tabel, `DataAdapter` pa pri ponudnikih, ki ga podpirajo, sodeluje pri prenosu podatkov. Teh razredov ni treba implementirati; pri `Microsoft.Data.Sqlite` uporabljamo seznam in ne neobstoječega `SqliteDataAdapter`.

## 8. Preveri omejitve in poišči napako — 10 točk

*Predavanje: poglavja 3, 7 in 14. Okvirno 10 minut.*

Naslednje poskuse izvajajte ločeno. Namenoma napačne ukaze shranite v ločeni datoteki, ne v običajni pripravljalni skript.

**A. Omejitvi zbirke.** V `napaka-kljuc.sql` napišite poskus vnosa avtorja z že obstoječim `Id = 1`. V `napaka-avtor.sql` napišite poskus vnosa knjige z novim `Id = 99`, poljubnim nepraznim naslovom, letom 2026 in neobstoječim `AvtorId = 999`. Vsak skript posebej zaženite z `--sql`. Oba morata biti zavrnjena; po vsakem znova preverite osnovne podatke. Začetno orodje izjemo izpiše kot napako programa; prestrezanja vseh izjem ni treba dodajati.

**B. Vsebinsko napačna povezava.** V kopiji poizvedbe iz naloge 4 začasno uporabite `ON k.Id = a.Id`. Napovejte in preverite avtorja tretje knjige. Pojasnite, zakaj je poizvedba izvedljiva, rezultat pa napačen. Obnovite pravilno povezavo. Pogleda zaradi tega poskusa ne spreminjajte.

**C. Napačna datoteka.** Začasno nastavite pot običajnega branja na neobstoječo datoteko, na primer `manjkajoca-knjiznica.db`, ob načinu `ReadOnly`. Zaženite program brez argumentov. Odpiranje mora odpovedati; nato obnovite pravo pot. Zakaj to ni isto kot uspešna poizvedba za leto 2030?

V kratko preglednico zapišite vhod oziroma spremembo, pričakovano vedenje, opaženo vedenje in vzrok. Dodajte rezultate pravilnega branja za leti 2024 in 2030 ter poskusa 3 → 0 → 3 iz naloge 7. Po obnovi pravilne kode ponovno izvedite običajni zagon.

**Na zagovoru:** ločite napako omejitve, napačne povezave tabel in dostopa do datoteke. Pojasnite, zakaj bi bil prazen seznam kot nadomestilo za vsako napako zavajajoč. Prikažite en uspešen in en neuspešen primer.

## Oddaja in zagovor

Oddajte projekt z izvorno kodo, datoteko projekta, datotekami SQL in kratkim zapisom odgovorov ter preverjanj. Priložite tudi svojo učno datoteko `knjiznica.db`; skripta `priprava.sql` in `pogled.sql` morata omogočiti ponovno pripravo enake zbirke v novi mapi. Map `bin` in `obj` ni treba oddati.

Končna koda naj uporablja pravilno pot in pravilni `JOIN`. Namerno napačni skripti naj bodo jasno poimenovani in naj se ne izvajajo pri običajnem zagonu. Zapis je lahko `odgovori.md` ali drug berljiv dokument; zadostujejo kratke razlage.

Na zagovoru boste sledili podatku od tabele prek poizvedbe in bralnika do objekta ter izpisa. Izvajalec lahko zahteva majhno spremembo, na primer drugo leto filtra ali drugačen vrstni red rezultatov. Novega sistema, obrazca ali zahtevnejše podatkovne arhitekture ne izdelujete.

## Dodatek: začetna koda za Program.cs

To kodo lahko neposredno uporabite. Z njo izvajate lastne datoteke SQL. Veja `--pripravi` uporablja `ExecuteNonQuery`, veja `--sql` prikaže vse rezultate datoteke, običajen zagon pa je namenjen vašim nalogam 5–7. Pri `--sql` je dovoljen zapis v obstoječo učno zbirko, da lahko ustvarite pogled in preizkusite zavrnjene vnose.

```csharp
using Microsoft.Data.Sqlite;

string pot = Path.GetFullPath("knjiznica.db");
Console.WriteLine($"Zbirka: {pot}");

bool priprava = args.Length == 1 && args[0] == "--pripravi";
bool sqlDatoteka = args.Length == 2 && args[0] == "--sql";

SqliteOpenMode nacin = priprava
    ? SqliteOpenMode.ReadWriteCreate
    : sqlDatoteka ? SqliteOpenMode.ReadWrite : SqliteOpenMode.ReadOnly;

string povezovalniNiz = new SqliteConnectionStringBuilder
{
    DataSource = pot,
    Mode = nacin,
    ForeignKeys = true
}.ToString();

if (priprava)
{
    string sql = File.ReadAllText("priprava.sql");
    using var povezava = new SqliteConnection(povezovalniNiz);
    povezava.Open();
    using var ukaz = povezava.CreateCommand();
    ukaz.CommandText = sql;
    ukaz.ExecuteNonQuery();
    Console.WriteLine("Priprava je končana.");
    return;
}

if (sqlDatoteka)
{
    string sql = File.ReadAllText(args[1]);
    using var povezava = new SqliteConnection(povezovalniNiz);
    povezava.Open();
    using var ukaz = povezava.CreateCommand();
    ukaz.CommandText = sql;
    using var reader = ukaz.ExecuteReader();
    do
    {
        if (reader.FieldCount == 0) continue;
        for (int i = 0; i < reader.FieldCount; i++)
        {
            Console.Write(reader.GetName(i) + " | ");
        }
        Console.WriteLine();
        while (reader.Read())
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                string vrednost = reader.IsDBNull(i)
                    ? "NULL" : reader.GetValue(i).ToString()!;
                Console.Write(vrednost + " | ");
            }
            Console.WriteLine();
        }
        Console.WriteLine();
    } while (reader.NextResult());
    Console.WriteLine("Izvajanje SQL je končano.");
    return;
}

if (args.Length != 0)
{
    Console.WriteLine("Uporaba: dotnet run [-- --pripravi | -- --sql ime.sql]");
    return;
}

// Sem dodajte svojo kodo za običajni zagon: naloge 5, 6 in 7.
// Uporabite zgoraj pripravljeni povezovalniNiz za branje.
```

Pri pomožni kodi `NextResult()` preide na rezultat naslednjega ukaza v isti datoteki. `IsDBNull` omogoči prikaz manjkajoče vrednosti. Klicaj za `ToString()` utiša opozorilo prevajalnika o možnem `null`; ne spreminja podatka. Te podrobnosti orodja niso samostojna ocenjevana naloga. Na zagovoru se osredotočite na lastni SQL, branje in razumevanje povezave.
