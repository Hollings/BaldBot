using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MelonLoader;
using Spacewood.Debugging;
using UnityEngine;

namespace ClassLibrary1
{
    internal class RoundData
    {
        public string roundJsonDataLocation = @"D:/Steam Games/steamapps/common/Super Auto Pets/Mods/data.json";
        public Dictionary<int, Dictionary<string, float>> roundPickRateCache = new Dictionary<int, Dictionary<string, float>>();
        
        public class RoundSerializerList
        {
            public List<RoundSerializer> roundSerializers = new List<RoundSerializer>();
        }

        public class RoundSerializer
        {
            public string playerName;
            public string teamName;
            public string hat;
            public int round;
            public string outcome;

            public List<TeamAnimalSerializer> animals = new List<TeamAnimalSerializer>();

        }

        [System.Serializable]
        public class TeamAnimalSerializer
        {
            public string name;
            public int attack;
            public int health;
            public int level;
            public int position;
            public int exp;
        }

        public RoundSerializerList getRoundSerializerList()
        {
            var roundJsonString = System.IO.File.ReadAllText(roundJsonDataLocation);
            var roundJsonData = JsonConvert.DeserializeObject<RoundSerializerList>(roundJsonString);
            return roundJsonData;
        }

        public void addRound(RoundSerializer roundSerializer)
        {
            var roundJsonData = getRoundSerializerList();
            roundJsonData.roundSerializers.Add(roundSerializer);
            var json = JsonConvert.SerializeObject(roundJsonData);
            System.IO.File.WriteAllText(roundJsonDataLocation, json);
        }

        public Dictionary<string, float> getPickRatesForAnimalsRound(int round)
        {
            float totalRounds = 0;
            Dictionary<string, float> pickedRoundsAnimals = new Dictionary<string, float>();
            foreach(RoundSerializer roundSerializer in getRoundSerializerList().roundSerializers)
            {
                if(roundSerializer.round == round)
                {
                    totalRounds++;
                    foreach(TeamAnimalSerializer roundAnimal in roundSerializer.animals)
                    {
                        //if the animal is not in the dictionary, add it
                        if(!pickedRoundsAnimals.ContainsKey(roundAnimal.name))
                        {
                            pickedRoundsAnimals.Add(roundAnimal.name, 1f);
                        }
                        //if the animal is in the dictionary, add 1 to the value
                        else
                        {
                            pickedRoundsAnimals[roundAnimal.name]++;
                        }
                    }
                }
            }
            var returnDict = new Dictionary<string, float>();
            // for each animal, divide the number of times it was picked by the total number of rounds
            Debug.Log("=== ROUND " + round + " ===");
            foreach(KeyValuePair<string, float> animal in pickedRoundsAnimals)
            {
                returnDict[animal.Key] = animal.Value / totalRounds;
                Debug.Log(animal.Key + ": " + (100*animal.Value / totalRounds));
            }

            return returnDict;
        }
        public float getPickRateForAnimalRound(string animal, int round)
        {
            // if round not in roundPickRateCache
            if(!roundPickRateCache.ContainsKey(round))
            {
                var roundData = getPickRatesForAnimalsRound(round);
                roundPickRateCache.Add(round, roundData);
            }
            if (roundPickRateCache[round].ContainsKey(animal))
            {
                return roundPickRateCache[round][animal];
            }
            return 0f;
            
        }
    }
}
