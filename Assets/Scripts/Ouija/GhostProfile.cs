using UnityEngine;

namespace Ouija
{
    [CreateAssetMenu(menuName = "Ouija/Ghost Profile", fileName = "Ghost Profile")]
    public class GhostProfile : ScriptableObject
    {
        [Header("Identity")] [SerializeField] private string ghostName = "Elisabeth van Dam";
        [SerializeField] private int deathYear = 1887;
        [SerializeField] private int ageAtDeath = 34;
        [SerializeField] private string causeOfDeath = "drowned in the canal behind the house";
        [SerializeField] private string temperament = "mournful and curious about the living";
        [SerializeField] private string secret = "hid a wedding ring under the attic floorboards";

        [Header("Randomize Pools")] [SerializeField]
        private string[] firstNames =
        {
            "Elisabeth", "Johanna", "Maria", "Cornelia", "Geertruida", "Aaltje", "Anna", "Hendrik", "Jan", "Pieter",
            "Willem", "Cornelis", "Gerrit", "Klaas"
        };

        [SerializeField] private string[] lastNames =
        {
            "van Dam", "de Vries", "Bakker", "Visser", "Smit", "Mulder", "de Wit", "van den Berg", "Hoekstra",
            "Kuiper", "Brouwer", "Vos"
        };

        [SerializeField] private Vector2Int deathYearRange = new(1650, 1985);
        [SerializeField] private Vector2Int ageRange = new(16, 90);

        [SerializeField] private string[] causes =
        {
            "drowned in the canal behind the house", "burned in a fire that started in the kitchen",
            "fell down the cellar stairs", "died of fever during a bitter winter",
            "were poisoned by a jealous relative", "froze to death searching for a lost child",
            "were crushed when the barn collapsed", "were hanged for a crime you did not commit"
        };

        [SerializeField] private string[] temperaments =
        {
            "mournful and curious about the living", "angry and territorial", "playful but cruel",
            "confused and convinced you are still alive", "protective of the house", "bitter and secretive"
        };

        [SerializeField] private string[] secrets =
        {
            "hid a wedding ring under the attic floorboards", "know who really started the fire",
            "buried a box of letters in the garden", "had a child nobody knew about",
            "stole money from the church", "saw something in the cellar that should not exist"
        };

        public string GhostName => ghostName;
        public int DeathYear => deathYear;
        public int AgeAtDeath => ageAtDeath;
        public string CauseOfDeath => causeOfDeath;
        public string Temperament => temperament;
        public string Secret => secret;
        public string DisplayName => $"{ghostName} (died {deathYear}, aged {ageAtDeath}), {temperament}";

        public void Randomize(int seed)
        {
            System.Random random = new(seed);
            ghostName = $"{Pick(random, firstNames)} {Pick(random, lastNames)}";
            deathYear = random.Next(deathYearRange.x, deathYearRange.y + 1);
            ageAtDeath = random.Next(ageRange.x, ageRange.y + 1);
            causeOfDeath = Pick(random, causes);
            temperament = Pick(random, temperaments);
            secret = Pick(random, secrets);
        }

        private static string Pick(System.Random random, string[] pool)
        {
            return pool[random.Next(pool.Length)];
        }

        [ContextMenu("Randomize")]
        public void RandomizeContextMenu()
        {
            Randomize(Random.Range(0, 10000000));
        }
    }
}
