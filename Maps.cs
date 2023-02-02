using System.Collections.Generic;

class Maps
{
    public static Dictionary<int, int> Points = new()
    {
        { 1, 50 },
        { 2, 45 },
        { 3, 40 },
        { 4, 35 },
        { 5, 30 },
        { 6, 25 },
        { 7, 22 },
        { 8, 19 },
        { 9, 16 },
        { 10, 13 },
        { 11, 10 },
        { 12, 8 },
        { 13, 6 },
        { 14, 4 },
        { 15, 2 }
    };

    public static Dictionary<int, string> Cars = new()
    {
        { 0, "Porsche 911 GT3 R"},
        { 1, "Mercedes-AMG GT3" },
        { 2, "Ferrari 488 GT3" },
        { 3, "Audi R8 LMS GT3" },
        { 4, "Lamborghini Huracan GT3" },
        { 5, "McLaren 650S GT3" },
        { 6, "Nissan GT-R Nismo GT3 (2018)" },
        { 7, "BMW M6 GT3" },
        { 8, "Bentley Continental GT3 (2018)" },
        { 9, "Porsche 911II Cup GT3" },
        { 10, "Nissan GT-R Nismo GT3 (2015)" },
        { 11, "Bentley Continental GT3 (2015)" },
        { 12, "Aston Martin V12 GT3" },
        { 13, "Reiter R-EX GT3" },
        { 14, "Emil Frey Jaguar G3" },
        { 15, "Lexus RC F GT3" },
        { 16, "Lamborghini Huracan GT3 EVO" },
        { 17, "Honda NSX GT3" },
        { 18, "Lamborghini Huracan ST" },
        { 19, "Audi R8 LMS EVO" },
        { 20, "AMR V8 Vantage GT3" },
        { 21, "Honda NSX GT3 Evo" },
        { 22, "McLaren 720S GT3" },
        { 23, "Porsche 991 II GT3 R" },
        { 24, "Ferrari 488 GT3 Evo" },
        { 25, "Mercedes-AMG GT3 Evo" },
        { 30, "BMW M4 GT3" },
        { 31, "Audi R8 LMS GT3 Evo II" },
        { 50, "Alpine A110 GT4" },
        { 51, "Aston Martin V8 Vantage GT4" },
        { 52, "Audi R8 LMS GT4" },
        { 53, "BMW M4 GT4" },
        { 55, "Chevrolet Camaro GT4.R" },
        { 56, "Ginetta G55 GT4" },
        { 57, "KTM X-Bow GT4" },
        { 58, "Maserati Granturismo MC GT4" },
        { 59, "McLaren 570S GT4" },
        { 60, "Mercedes AMG GT4" },
        { 61, "Porsche 718 Cayman GT4 Clubsport" }
    };

    public enum Classes
    {
        pro = 3,
        silver = 1,
        am = 0
    }
}