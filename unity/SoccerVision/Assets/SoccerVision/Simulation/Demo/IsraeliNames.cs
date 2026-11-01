using System;
using System.Collections.Generic;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// The demo squads' names, drawn from the name databases (data/names: Israeli, Spanish, American; duplicates removed - a
    /// name may still be both a first name and a surname). Every seed names the players anew. A squad is Israeli, with
    /// <see cref="ForeignPlayers"/> foreigners in the whole match - half Spanish, half American - spread over both squads;
    /// the two squads never share a surname, and the managers are Israeli.
    /// </summary>
    public static class IsraeliNames
    {
        /// <summary>Foreign players in a match (7 Spanish and 7 American) among the 40 of the two squads.</summary>
        public const int ForeignPlayers = 14;

        public static readonly string[] IsraelFirst =
        {
            "Avi", "Avior", "Aviel", "Aviv", "Avigad", "Avidor", "Avidan", "Avihu", "Avichai", "Avihan", "Avimelech",
            "Avinadav", "Avinoam", "Aviner", "Avinatan", "Aviad", "Aviezer", "Aviam", "Avikam", "Abir", "Aviraz",
            "Aviram", "Aviran", "Avishor", "Avishai", "Eviatar", "Avner", "Avshalom", "Adam", "Adir", "Hadar",
            "Ohad", "Ehud", "Eliav", "Aaron", "Ofir", "Ofek", "Or", "Orel", "Ori", "Oren", "Orian", "Osher", "Oshri",
            "Eyal", "Eilon", "Ilai", "Ilan", "Itamar", "Eitan", "Elad", "Eliad", "Assa", "Elisha", "Almog", "Amir",
            "Amitai", "Amit", "Elroi", "Arik", "Asaf", "Erez", "Afik", "Efraim", "Arie", "Ari", "Ariel", "Arnon",
            "Asher", "Baari", "Ben", "Benjamin", "Benia", "Boaz", "Bezalel", "Baruch", "Bar", "Boris", "Barak",
            "Gabriel", "Gavriel", "Gad", "Gideon", "Golan", "Goel", "Gershom", "Dov", "Dvir", "Dovev", "David",
            "Dolev", "Dor", "Doron", "Doriel", "Dotan", "Din", "Dan", "Daniel", "Dekel", "Dror", "Hertzel", "Zeev",
            "Zoar", "Ziv", "Haviv", "Hagai", "Tal", "Tal Or", "Talmor", "Dimitri", "Haim", "Hanoch", "Yair", "Igal",
            "Yagil", "Iahav", "Yeuda", "Yonatan", "Yoni", "Yoel", "Yohai", "Yoav", "Yuval", "Yogev", "Yohanan",
            "Yona", "Adi", "Yosef", "Yoram", "Yotam", "Izhar", "Yehezkel", "Yehiel", "Yam", "Yanai", "Inon", "Ianir",
            "Yaakov", "Itzhak", "Yakir", "Yarden", "Yaron", "Yariv", "Yarin", "Ishai", "Kfir", "Carmi", "Liel",
            "Lior", "Lev", "Levi", "Leon", "Lidor", "Liron", "Mor", "Maor", "Meir", "Meidan", "Menachem",
            "Mordechai", "Matan", "Hen", "Naor", "Nitzan", "Nevo", "Nimrod", "Nissan", "Nir", "Nitai", "Noam",
            "Naftali", "Neria", "Natan", "Netanel", "Stav", "Slav", "Slava", "Ido", "Ofer", "Omri", "Eran", "Peleg",
            "Paz", "Pinhas", "Tzvi", "Israel", "Koren", "Reem", "Ravid", "Roi", "Roei", "Rom", "Roni", "Rotem",
            "Rafael", "Raz", "Ran", "Shaul", "Shaanan", "Sagi", "Shoam", "Shimshon", "Shlomo", "Shaked", "Tomer",
            "Tom", "Tamir", "Guy", "Yedidia", "Dudu", "Nissim", "Shahar", "Shimi", "Omer", "Shalom", "Shalev",
        };

        public static readonly string[] IsraelLast =
        {
            "Shaul", "Aviel", "Avivi", "Aviv", "Avigur", "Avidan", "Avinoam", "Aviad", "Avishai", "Even", "Avner",
            "Egozi", "Admoni", "Uriel", "Zuriel", "Eyalon", "Ilan", "Eldad", "Elul", "Malul", "Efrat", "Argov",
            "Argaman", "Arad", "Arazi", "Harazi", "Arieli", "Ashkenazi", "Etgar", "Ben Ari", "Ben Baruch", "Barak",
            "Ben Gera", "Ben Dayan", "Ben Hur", "Ben Zaken", "Ben David", "Ben Haim", "Ben Yehuda", "Shem Tov",
            "Ben Nun", "Ben Porat", "Ben Zur", "Bentov", "Ben Shushan", "Banai", "Bar Or", "Bar Natan", "Barel",
            "Barzilai", "Bracha", "Barkat", "Gaon", "Gabai", "Geva", "Gad", "Gadot", "Gedalia", "Gonen", "Goren",
            "Ziv", "Zcharia", "Gil", "Gazit", "Ginosar", "Gilboa", "Galil", "Gafni", "Gershon", "Dovrat", "Dagan",
            "Dahan", "Vardi", "Zeev", "Zvulun", "Zahavi", "Zoar", "David", "Davidi", "Dolev", "Dor", "Dori", "Drori",
            "Dayan", "Dar", "Golan", "Zamir", "Zakai", "Hermon", "Tuvia", "Tovim", "Tal", "Talmon", "Yavin", "Yagur",
            "Tadin", "Yedidia", "Iaffa", "Ifrah", "Yefet", "Itzhak", "Itzhaki", "Yekutiel", "Yekutieli", "Yardeni",
            "Yarden", "Yerushalmi", "Hetzroni", "Ishai", "Israel", "Israeli", "Cohen", "Cochav", "Caspi", "Lev Ari",
            "Lotem", "Levi", "Levian", "Lish", "Lapid", "Leshem", "Meir", "Maor", "Megido", "Magel", "Moria",
            "Mazor", "Matmon", "Madmon", "Meitar", "Malki", "Melamed", "Manor", "Solomon", "Menachem", "Marom",
            "Meshulam", "Noi", "Noam", "Unger", "Nuriel", "Nachum", "Nehama", "Nahshon", "Nitzan", "Nitai", "Nimrod",
            "Netzer", "Nativ", "Ovad", "Ovadia", "Oz", "Uziel", "Omer", "Atzili", "Dutov", "Davidov", "Azuz", "Amir",
            "Amit", "Amrami", "Amrani", "Peer", "Porat", "Polek", "Gozlan", "Paz", "Zfadya", "Peled", "Pereg",
            "Tzabar", "Zadok", "Tzur", "Kadmon", "Kehat", "Katan", "Kishon", "Katzav", "Kashti", "Raviv", "Ravid",
            "Rahav", "Revah", "Sherf", "Raz", "Raziel", "Ramon", "Remez", "Ratzon", "Reshef", "Shauli", "Shmuel",
            "Shavit", "Shabtai", "Shabat", "Segev", "Sagi", "Shor", "Shurk", "Shushan", "Schori", "Shilon", "Shiri",
            "Tamir", "Tikva", "Tamuz", "Tel Zur", "Tirosh", "Tor", "Tadmor", "Tagar", "Tavori", "Tavor", "Sharon",
            "Shkalim", "Shaked", "Sahar", "Shemesh", "Shivhon", "Shalem", "Shalom", "Sharabi",
            "Israeli arabs first name:", "Muhammad", "Hamudi", "Ahmad", "Hatem", "Walid", "Said", "Daud", "Moodi",
            "Shafik", "Samir", "Omar", "Moanes", "Anas", "Tamer", "Taufik", "Mustafa", "Nazer", "Nazir", "Abed",
            "Yehya", "Salah", "Hisham", "Wasim", "Amin", "Dia", "Mahdi", "Israeli arabs last name:", "Abu Muhammad",
            "Abdul", "Abdalla", "Abu Amar", "Abu Saif", "Dabur", "Hamed", "Elhamed", "Abu Said", "Abu Farhi",
            "Farhi", "Kanaan", "Haled", "Abu hamza", "Ali", "Abu Ali", "Abu Ahmed", "Abu aladdin", "Aladdin",
            "Gadir", "Badir", "Havivallah", "Allah", "Guamis", "Abu Salah", "Laius", "Abu Hadir", "Saba",
            "Abu Zkika", "Turk", "Abu Kaf", "El Ziadat", "Zbidet", "Kayal", "Asaad", "Israeli russian first name:",
            "Slava", "Maxim", "Dimitri", "Anton", "Sasha", "Michael", "Ilia", "Stas", "Leon", "Misha", "Boris",
            "Leonid", "Ariel", "Victor", "Artem", "Igor", "Israeli russian last name:", "Zabrovski", "Polanski",
            "Projanski", "Savranski", "Popov", "Petrov", "Plutkin", "Ivanov", "Romanov", "Zvulunov", "Makov",
            "Avrutkin", "Babian", "Ponomeranko", "Israeli Ethiopian first name:", "Warko", "Avram", "Addisu", "Abai",
            "Abebe", "Afework", "Aforke", "Zelalem", "Zewdu", "Dawit", "Isayas", "Eli", "Aaron", "Yonas", "Yarga",
            "Israeli Ethiopian last name:", "Dassa", "Adane", "Bekele", "Gebre", "Habte", "Kebede", "Mesfin",
            "Mengistu", "Tefera", "Dgania", "Takala", "Kasa", "Yaakov", "Tzagai", "Tzaga", "Mula",
        };

        public static readonly string[] SpanishFirst =
        {
            "Alberto", "Alejandro", "Alfonso", "Carlos", "David", "Diego", "Emanuel", "Ezra", "Gabriel", "Immanuel",
            "Isaac", "Manuel", "Marcos", "Mateo", "Rafael", "Salvador", "Ruben", "Samuele", "Samuel", "Vidal",
            "Mercando", "Alvaro", "Andres", "Angel", "Antonio", "Asier", "Bautista", "Beltran", "Benjamin",
            "Bernabé", "Borja", "Cayetano", "Cesar", "Cristobal", "Daniel", "Damian", "Dario", "Domingo", "Eduardo",
            "Emilio", "Eneko", "Enzo", "Enrique", "Ernesto", "Esteban", "Eufemio", "Fausto", "Federico", "Feliciano",
            "Felipe", "Felix", "Fermin", "Fernando", "Fidel", "Gabino", "Gael", "Gaspar", "Gaston", "Gil", "Gonzalo",
            "Gregorio", "Guillermo", "Hector", "Guzman", "Hernan", "Hilario", "Hugo", "Iago", "Iban", "Ignacio",
            "Iker", "Ismael", "Israel", "Jairo", "Javier", "Jesus", "Joaquin", "Joel", "Jordi", "Jorge", "Jose",
        };

        public static readonly string[] SpanishLast =
        {
            "Aguilar", "Alba", "Alcala", "Alonso", "Amador", "Aragones", "Arias", "Arroyo", "Ayala", "Baez",
            "Ballesteros", "Barreto", "Barrios", "Beltran", "Benitez", "Blanco", "Bonilla", "Bravo", "Burgos",
            "Bustamante", "Caballero", "Cabrera", "Calderon", "Camacho", "Campos", "Cano", "Cardenas", "Carmona",
            "Carvajal", "Casanova", "Castaño", "Castillo", "Castro", "Cervantes", "Chavez", "Cifuentes", "Collado",
            "Cordoba", "Coronado", "Cortes", "Crespo", "Cruz", "Delgado", "Diaz", "Diez", "Dominguez", "Duran",
            "Echeverria", "Enriquez", "Escalante", "Escobar", "Espinoza", "Esteban", "Fajardo", "Fernandez",
            "Ferrer", "Figueroa", "Flores", "Fonseca", "Franco", "Fuentes", "Gaitan", "Gallardo", "Gallego",
            "Garcia", "Garrido", "Garza", "Gil", "Gimenez", "Gomez", "Gonzalez", "Gracia", "Granados", "Guerrero",
            "Gutierrez", "Guzman", "Haro", "Hernandez", "Herrera", "Hidalgo", "Huerta", "Hurtado", "Ibanez",
            "Iglesias", "Infante", "Izquierdo", "Jaimes", "Jimenez", "Jordan", "Lara", "Larios", "Leon", "Lerma",
            "Linares", "Lira", "Llorente", "Lopez", "Lorenzo", "Lozano",
        };

        public static readonly string[] AmericanFirst =
        {
            "James", "John", "Robert", "Michael", "William", "David", "Richard", "Joseph", "Thomas", "Christian",
            "Walter", "Keith", "Austin", "Roger", "Terry", "Sean", "Gerald", "Carl", "Dylan", "Harold", "Jordan",
            "Jesse", "Bryan", "Lawrence", "Arthur", "Gabriel", "Bruce", "Logan", "Billy", "Albert", "Willie", "Alan",
            "Juan", "Wayne", "Elijah", "Randy", "Roy", "Vincent", "Ralph", "Eugene", "Russell", "Bobby", "Mason",
            "Philip", "Charles", "Christopher", "Daniel", "Matthew", "Anthony", "Mark", "Donald", "Steven", "Paul",
            "Andrew", "Joshua", "Kenneth", "Kevin", "Brian", "George", "Timothy", "Ronald", "Edward", "Jason",
            "Jeffrey", "Ryan", "Jacob", "Gary", "Nicholas", "Eric", "Jonathan", "Stephen", "Larry", "Justin",
            "Scott", "Brandon", "Benjamin", "Samuel", "Gregory", "Alexander", "Frank", "Patrick", "Raymond", "Jack",
            "Dennis", "Jerry", "Tyler", "Aaron", "Adam", "Nathan", "Henry", "Douglas", "Zachary", "Peter", "Kyle",
            "Ethan", "Noah", "Jeremy",
        };

        public static readonly string[] AmericanLast =
        {
            "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez",
            "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin",
            "Lee", "Perez", "Thompson", "White", "Harris", "Sanchez", "Clark", "Ramirez", "Lewis", "Robinson",
            "Walker", "Young", "Allen", "King", "Wright", "Scott", "Torres", "Nguyen", "Hill", "Flores", "Green",
            "Adams", "Nelson", "Baker", "Hall", "Rivera", "Campbell", "Mitchell", "Carter", "Roberts", "Gomez",
            "Phillips", "Evans", "Turner", "Diaz", "Parker", "Cruz", "Edwards", "Collins", "Reyes", "Stewart",
            "Morris", "Morales", "Murphy", "Cook", "Rogers", "Gutierrez", "Ortiz", "Morgan", "Cooper", "Peterson",
            "Bailey", "Reed", "Kelly", "Howard", "Ramos", "Kim", "Cox", "Ward", "Richardson", "Watson", "Brooks",
            "Chavez", "Wood", "James", "Bennett", "Gray", "Mendoza", "Ruiz", "Hughes", "Price", "Alvarez", "Sanders",
            "Patel", "Myers", "Long", "Ross", "Foster",
        };

        /// <summary>First names and surnames of the Israelis (kept for the tests).</summary>
        public static string[] First => IsraelFirst;
        public static string[] Last => IsraelLast;

        /// <summary>
        /// Names the two squads of a match (starters, bench and managers) from its <paramref name="seed"/>. Call with the same
        /// seed for both teams: <paramref name="half"/> 0 (home) or 1 (away) decides which of the foreigners come to this team.
        /// </summary>
        public static void Name(TeamSetup team, ulong seed, int half)
        {
            var rng = new Random((int)((seed * 7919UL + (ulong)half * 104729UL) % int.MaxValue));
            var order = new Random((int)(seed % int.MaxValue));
            var il = Shuffled(IsraelLast, order); var sl = Shuffled(SpanishLast, order); var al = Shuffled(AmericanLast, order);
            int ik = half * (il.Count / 2), sk = half * (sl.Count / 2), ak = half * (al.Count / 2);
            var players = new List<PlayerProfile>(team.Starters);
            players.AddRange(team.Bench);
            // this team's foreigners: 7 of the 14 (alternating Spanish and American), as a shuffle of its 20 squad places
            int perTeam = ForeignPlayers / 2;
            var places = new List<int>();
            for (int i = 0; i < players.Count; i++) places.Add(i);
            var pick = new Random((int)((seed * 31UL + (ulong)half) % int.MaxValue));
            for (int i = places.Count - 1; i > 0; i--) { int j = pick.Next(i + 1); (places[i], places[j]) = (places[j], places[i]); }
            var foreign = new Dictionary<int, bool>(); // place -> Spanish?
            for (int i = 0; i < perTeam && i < places.Count; i++) foreign[places[i]] = (i + half) % 2 == 0;
            var used = new HashSet<string>();
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                string first, last;
                if (foreign.TryGetValue(i, out bool spanish))
                {
                    var fs = spanish ? SpanishFirst : AmericanFirst;
                    first = fs[rng.Next(fs.Length)];
                    var ls = spanish ? sl : al; int k = spanish ? sk++ : ak++;
                    do last = ls[k++ % ls.Count]; while (!used.Add(last) && k < 4 * ls.Count);
                    p.Nationality = spanish ? "Spain" : "USA";
                }
                else
                {
                    first = IsraelFirst[rng.Next(IsraelFirst.Length)];
                    int k = ik++;
                    do last = il[k++ % il.Count]; while (!used.Add(last) && k < 4 * il.Count);
                    ik = k;
                    p.Nationality = "Israel";
                }
                p.FirstName = first;
                p.LastName = last;
            }
            team.Manager = IsraelFirst[rng.Next(IsraelFirst.Length)] + " " + il[ik++ % il.Count];
        }

        private static List<string> Shuffled(string[] a, Random r)
        {
            var l = new List<string>(a);
            for (int i = l.Count - 1; i > 0; i--) { int j = r.Next(i + 1); (l[i], l[j]) = (l[j], l[i]); }
            return l;
        }
    }
}
