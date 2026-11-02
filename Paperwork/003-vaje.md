# Vaje 3: podatki, funkcije in moduli

V jeziku C# ustvarite konzolni projekt `Vaja003`. Pomagajte si s [predavanjem](003-predavanje.md) in dokumentacijo C#. Svojo kodo morate znati pojasniti.

## 1. Pretvorba časa

Napovejte izpis in ga preverite z zagonom:

```csharp
int a = 10;
int b = a;
a = a + 5;
Console.WriteLine(b);
Console.WriteLine(7 / 2);
Console.WriteLine(7 / 2.0);
```

Napišite funkcijo `static string OblikujTrajanje(int skupnoSekund)`, ki sekunde pretvori v besedilo z urami, minutami in sekundami. Primer: `7322` → `2 h 2 min 2 s`. Število ur lahko presega 24.

Uporabite konstanti za število sekund v minuti in uri ter deljenje in ostanek pri deljenju. 

## 2. Cena dostave

Napišite funkcijo `static decimal CenaDostave(decimal masaKg, bool prednostno)` po ceniku:

| Masa paketa                 | Cena  |
| --------------------------- | -----:|
| Več kot 0 do vključno 1 kg  | 3 EUR |
| Več kot 1 do vključno 5 kg  | 5 EUR |
| Več kot 5 do vključno 20 kg | 9 EUR |

Prednostna dostava doda 2 EUR.

## 3. Obdelava meritev

Za polje `double[] meritve = { 18, 20, 19, 23, 20 };` napišite funkcije:

```csharp
static double Povprecje(double[] meritve)
static double Najvecja(double[] meritve)
static int PrestejNad(double[] meritve, double meja)
```

Funkcije naj vrnejo povprečje, največjo vrednost in število meritev, strogo večjih od meje. Uporabite zanke, brez LINQ in spreminjanja vhodnega polja.

Za podano začetno polje pričakujemo povprečje 20, največjo vrednost 23 in pri meji 20 eno meritev nad mejo. 

## 4. Modul statistike

Funkcije iz naloge 3 prestavite v datoteko `Statistika.cs`, v razred `public static class Statistika`. Metode naj bodo `public static`.

V `Program.cs` pripravite podatke, pokličite metode in izpišite rezultate. Modul naj vsebuje samo izračune, brez konzolnega vnosa, izpisa ali skupnega spremenljivega stanja.

Za vsako metodo na kratko navedite vhod, rezultat in obnašanje pri praznem polju. Ponovite preverjanja iz naloge 3. Pojasnite pomen `public` in `static` ter kako bi modul uporabili v neki aplikaciji.

## 5. Poiščite in popravite napake

Vsak izsek obravnavajte ločeno: napovejte posledico, preverite izvajanje oziroma sporočilo prevajalnika, pojasnite vzrok in popravite kodo.

```csharp
int stevilo = 3;
if (stevilo > 0
{
    Console.WriteLine("Število je pozitivno.");
}
```

Pri naslednjem izseku želimo rezultat 2,5:

```csharp
int vsota = 5;
int stevilo = 2;
double rezultat = vsota / stevilo;
```

Naslednja zanka naj izpiše vse elemente brez napake:

```csharp
int[] tocke = { 10, 20, 30 };
for (int i = 0; i <= tocke.Length; i++)
{
    Console.WriteLine(tocke[i]);
}
```

Pri zadnjem izseku želimo izpis 11. Popravite funkcijo in klic z uporabo vrnjene vrednosti, brez `ref` ali skupne spremenljivke:

```csharp
static void DodajTocko(int tocke)
{
    tocke++;
}

int rezultatIgre = 10;
DodajTocko(rezultatIgre);
Console.WriteLine(rezultatIgre);
```

## 6. Ugibanje števila

Izdelajte igro ugibanja naključnega celega števila od 1 do 100.

- Uporabnik ima največ sedem veljavnih poskusov.
- Po ugibanju izpišite »premajhno«, »preveliko« ali »pravilno«.
- Neštevilski vnosi in števila zunaj območja ne štejejo kot poskus.
- Ob pravilnem odgovoru igro takoj končajte; ob porabljenih poskusih razkrijte število.
- Če `Console.ReadLine()` vrne `null`, končajte program.

Za preverjanje začasno uporabite iskano število 42. Preverite uspeh, neuspeh in neveljavne vnose, nato vključite naključno izbiro. Pojasnite, zakaj strategija razpolavljanja območja zadošča za rešitev v sedmih poskusih.
