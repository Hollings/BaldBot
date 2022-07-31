using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EasyMobile;
using MelonLoader;
using Microsoft.Win32.SafeHandles;
using Spacewood.Core.Enums;
using Spacewood.Core.Models;
using Spacewood.Core.System;
using Spacewood.Unity;
using Spacewood.Unity.MonoBehaviours.Battle;
using Spacewood.Unity.MonoBehaviours.Board;
using Spacewood.Unity.MonoBehaviours.Build;
using Spacewood.Unity.UI;
using Spacewood.Unity.Views;
using UnityEngine;
using Object = UnityEngine.Object;
using SpaceWoodSpace = Spacewood.Unity.Views.Space;
using System.Data;
using System.IO;
using System.Data.Sql;
using Newtonsoft.Json;
using Random = UnityEngine.Random;

namespace ClassLibrary1
{
    public class MyMod : MelonMod
    {
        public string gameState = "None";

        // MAIN STUFF

        #region Main Stuff

        private float actionTimer = 3f;
        private static float defaultActionTime = 2f;
        private float nextActionTime = 2f;
        private bool modActive = false;
        private RoundData roundData;

        public override void OnLoaderInitialized()
        {
            roundData = new RoundData();
        }

        public override void OnGUI()
        {

            if (gameState == "Build")
            {
                BuildArenaGui();
            }

            if (gameState == "Menu")
            {
                BuildMenuGui();
            }

            buildGlobalGui();
        }

        public void buildGlobalGui()
        {
            // create button that toggles modActive
            if (GUI.Button(new Rect(10, 10, 400, 30), "Toggle Mod. Current State " + modActive))
            {
                modActive = !modActive;
            }
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            // LoggerInstance.Msg("Scene was initialized " + sceneName);

            gameState = sceneName;
            if (gameState == "Build")
            {
                SetupBuild();
            }

            if (gameState == "Menu")
            {
                round = 0;
                while (pageManager == null)
                {
                    SetupMenu();
                }

                roundData.roundPickRateCache = new Dictionary<int, Dictionary<string, float>>();
            }

            if (gameState == "Battle")
            {
                SetupBattle();
            }
        }

        public override void OnUpdate()
        {
            switch (gameState)
            {
                case "Build":
                    BuildPhase();
                    break;
                case "Menu":
                    MenuMode();
                    break;
                case "Battle":
                    BattleMode();
                    break;
            }
        }

        #endregion

        // BUILD MODE

        # region Build Mode

        public List<Space> HangarSpaces = new List<Space>();
        public List<Space> ShopSpaces = new List<Space>();
        BoardController boardController;
        BoardView boardView;
        HangarMain hangar;
        MinionArmy minionArmy;
        MinionShop minionShop;

        private HangarOverlay hangarOverlay;

        // private bool turnOver = false;
        List<QueuedAction> queuedActions = new List<QueuedAction>();
        public int round = 0;


        public class QueuedAction
        {
            public Action action;
            public string name;
            public string info;
            public float bufferTime;

            public QueuedAction(Action action, string name, string info, float bufferTime = -1f)
            {
                if (bufferTime < 0f)
                {
                    bufferTime = defaultActionTime;
                }

                this.action = action;
                this.name = name;
                this.info = info;
                this.bufferTime = bufferTime;
            }
        }

        // list of rects and their corresponding names
        public class Space
        {
            public SpaceWoodSpace space;
            public Animal animal;
            public Vector2 screenPosition;
            public int index;

            public void Click()
            {
                var pointerCapture = space.PointerCapture;
                space.HandleClick(pointerCapture);
            }
        }

        public class Animal
        {
            public string name;
            public Owner owner;
            public int health;
            public int attack;
            public int experience;
            public MinionModel minion;
            public float pickRateCache = -1f;
        }

        public void BuildPhase()
        {
            if (HangarSpaces.Count == 0)
            {
                SetupBuild();
            }

            actionTimer += Time.deltaTime;
            if (actionTimer > nextActionTime)
            {
                if (!queuedActions.Any())
                {
                    chooseNextAction();
                }
                else
                {
                    doQueuedAction();
                }
            }

        }

        public void doQueuedAction()
        {
            if (!queuedActions.Any()) return;
            // pop the first action off the queue and execute it
            var action = queuedActions.First();
            // LoggerInstance.Msg("Doing Queued Action: " + action.action.Method.Name + " " + action.info);
            queuedActions.Remove(action);
            action.action();
            nextActionTime = action.bufferTime;
            actionTimer = 0f;
            InitializeSpaces();
        }

        private void SetupBuild()
        {
            Time.timeScale = 1f;
            queuedActions.Clear();
            // LoggerInstance.Msg("setting up");
            boardController = BoardController._instance;
            if(boardController == null)
            {
                // LoggerInstance.Msg("board controller not ready");
                return;
            }
            boardView = boardController.BoardView;
            hangar = boardController.Hangar;
            minionArmy = hangar.MinionArmy;
            minionShop = hangar.MinionShop;
            hangarOverlay = hangar.Overlay;
            InitializeSpaces();
            waitAction(3);
            confirmShopTierUpgrade();
            waitAction(1);
            InitializeSpaces();
        }

        private void InitializeSpaces()
        {
            var roundText = int.Parse(hangar.Overlay.Turns.TextMesh.text);
            round = roundText;
            
            HangarSpaces.Clear();
            ShopSpaces.Clear();
            var i = 0;
            foreach (SpaceWoodSpace space in minionArmy.Spaces.Items)
            {
                var newSpace = new Space();
                var spacePosition = space.transform.position;
                var guiPosition = Camera.main.WorldToScreenPoint(spacePosition);
                guiPosition.y = Screen.height - guiPosition.y;
                newSpace.space = space;
                newSpace.screenPosition = guiPosition;
                newSpace.animal = null;
                newSpace.index = i;
                HangarSpaces.Add(newSpace);
                i++;
            }

            i = 0;
            foreach (SpaceWoodSpace space in minionShop.Spaces)
            {
                var newSpace = new Space();
                var spacePosition = space.transform.position;
                var guiPosition = Camera.main.WorldToScreenPoint(spacePosition);
                guiPosition.y = Screen.height - guiPosition.y;
                newSpace.space = space;
                newSpace.screenPosition = guiPosition;
                newSpace.animal = null;
                newSpace.index = i;
                ShopSpaces.Add(newSpace);
                i++;
            }

            PopulateAnimalList();
        }

        private void PopulateAnimalList()
        {
            // Hangar
            var minionsByPosition = hangar.BuildModel.Board.Minions.Items;
            var i = 0;
            foreach (var minion in minionsByPosition)
            {
                if (minion == null)
                {
                    HangarSpaces[i].animal = null;
                    i++;
                    continue;
                }

                var name = minion.Enum.ToString();
                var attack = minion.Attack.Total;
                var health = minion.Health.Total;
                var experience = minion.Exp;

                var animal = new Animal();
                animal.name = name;
                animal.owner = minion.Owner;
                animal.attack = attack;
                animal.health = health;
                animal.experience = experience;
                animal.minion = minion;
                HangarSpaces[i].animal = animal;
                i++;
            }

            // Shop
            var shopMinions = hangar.BuildModel.Board.MinionShop;
            i = 0;
            foreach (var minion in shopMinions)
            {
                var name = minion.Enum.ToString();
                var attack = minion.Attack.Total;
                var health = minion.Health.Total;
                var experience = minion.Exp;
                var animal = new Animal();
                animal.name = name;
                animal.owner = minion.Owner;
                animal.attack = attack;
                animal.health = health;
                animal.experience = experience;
                animal.minion = minion;
                ShopSpaces[i].animal = animal;
                i++;
            }
        }

        public void selectAnimal(int position, bool shop = false)
        {
            Space space = null;

            if (shop)
            {
                space = ShopSpaces[position];
            }
            else
            {
                space = HangarSpaces[position];
            }

            if (space != null)
            {
                queuedActions.Add(new QueuedAction(space.Click, "Select animal " + space.animal.name, "selectAnimal"));
            }
        }

        public void waitAction(int time)
        {
            queuedActions.Add(new QueuedAction(() => { }, "Wait for " + time, "waitAction", time));
        }

        public bool openSpace()
        {
            // loop through hangar spaces and return true if any are open
            foreach (var space in HangarSpaces)
            {
                if (space.animal == null)
                {
                    return true;
                }
            }

            return false;
        }

        public void sell(int position)
        {
            queuedActions.Add(new QueuedAction(HangarSpaces[position].Click, "Click Shop " + position, "Sell Animal",
                0.25f));
            queuedActions.Add(new QueuedAction(hangarOverlay.SellButton.Click, "Click Sell Button ", "Sell Animal"));
        }

        public void sellLowestAnimal()
        {
            // sell a random animal
            // todo - sell lowest pickrate and tier
            var shopPosition = Random.Range(0, 2);
            // get hangar animal with lowest exp
            var lowest_exp = 99;
            var lowest_index = -1;
            foreach (var space in HangarSpaces)
            {
                if (space.animal.experience < lowest_exp)
                {
                    lowest_exp = space.animal.experience;
                    lowest_index = space.index;
                }
            }

            sell(lowest_index);
        }

        public void mergeAnimal(int hangarPosition1, int hangarPosition2)
        {
            queuedActions.Add(new QueuedAction(HangarSpaces[hangarPosition1].Click, "Click Hangar " + hangarPosition1,
                "Merge Animal", 0.25f));
            queuedActions.Add(new QueuedAction(HangarSpaces[hangarPosition2].Click, "Click Hangar " + hangarPosition2,
                "Merge Animal"));
        }

        public void buyAnimal(int shopPosition, int hangarPosition, bool insert = false)
        {
            if (insert)
            {
                queuedActions.Insert(0,
                    new QueuedAction(ShopSpaces[shopPosition].Click, "Click Shop " + shopPosition, "Buy Animal",
                        0.25f));
                queuedActions.Insert(1,
                    new QueuedAction(HangarSpaces[hangarPosition].Click, "Click Hangar " + shopPosition, "Buy Animal"));
            }
            else
            {
                queuedActions.Add(new QueuedAction(ShopSpaces[shopPosition].Click, "Click Shop " + shopPosition,
                    "Buy Animal", 0.25f));
                queuedActions.Add(new QueuedAction(HangarSpaces[hangarPosition].Click, "Click Hangar " + shopPosition,
                    "Buy Animal"));
            }
        }

        public void chooseNextAction()
        {
            InitializeSpaces();
            if (!modActive)
            {
                return;
            }
            if (currentGold() >= 3)
            {
                if (openSpace())
                {
                    // find a shop space with a fish
                    var shopPosition = 0;
                    buyAnimal(shopPosition, 0);
                }
                else if (!upgradeAnimals())
                {
                    if (currentGold() == 10)
                    {
                        // 50/50 chance to sell an animal
                        if (Random.Range(0, 1) == 0)
                        {
                            sellLowestAnimal();
                            buyAnimal(0, Random.Range(0, 5));
                        }
                    }
                    else
                    {
                        rollShop();
                    }
                }
            }
            else if (currentGold() > 0)
            {
                rollShop();
                // todo freeze upgradeables
                // todo freeze highest tier shop animal
            }
            else
            {
                endTurn();
            }
        }

        public int currentGold()
        {
            return boardController.BoardView.Model.Gold;
        }

        public void endTurn()
        {
            queuedActions.Add(new QueuedAction(hangar.Overlay.DoneButton.Click, "click done", "end turn", 1));
            queuedActions.Add(new QueuedAction(hangar.Alert.ConfirmButton.Click, "click confirm", "end turn"));
        }

        public void rollShop()
        {
            queuedActions.Add(new QueuedAction(hangar.Overlay.Roll.Button.Click, "Roll", "rollshop()"));
        }

        public bool upgradeAvailable()
        {
            foreach (var hangarspace1 in HangarSpaces)
            {
                foreach (var hangarSpace2 in HangarSpaces)
                {
                    if (hangarspace1 == hangarSpace2)
                    {
                        continue;
                    }

                    if (hangarspace1.animal != null &&
                        hangarSpace2.animal != null &&
                        hangarSpace2.animal.name == hangarspace1.animal.name)
                    {
                        return true;
                    }
                }
            }

            // todo combine hangar animals, too
            foreach (var shopSpace in ShopSpaces)
            {
                foreach (var hangarSpace in HangarSpaces)
                {
                    if (shopSpace.animal != null &&
                        hangarSpace.animal != null &&
                        shopSpace.animal.name == hangarSpace.animal.name)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool upgradeAnimals()
        {
            // upgrade from hangar
            foreach (var hangarSpace1 in HangarSpaces)
            {
                foreach (var hangarSpace2 in HangarSpaces)
                {
                    if (hangarSpace1 == hangarSpace2)
                    {
                        continue;
                    }

                    if (hangarSpace1.animal != null &&
                        hangarSpace2.animal != null &&
                        hangarSpace2.animal.name == hangarSpace1.animal.name &&
                        hangarSpace1.animal.experience < 5 &&
                        hangarSpace2.animal.experience < 5)
                    {
                        mergeAnimal(hangarSpace2.index, hangarSpace1.index);
                        return true;
                    }
                }
            }

            // upgrade from shop
            foreach (var shopSpace in ShopSpaces)
            {
                foreach (var hangarSpace in HangarSpaces)
                {
                    if (shopSpace.animal != null &&
                        hangarSpace.animal != null &&
                        shopSpace.animal.name == hangarSpace.animal.name &&
                        hangarSpace.animal.experience < 5)
                    {
                        buyAnimal(shopSpace.index, hangarSpace.index, true);
                        return true;
                    }
                }
            }

            return false;
        }

        public void confirmShopTierUpgrade()
        {
            queuedActions.Add(new QueuedAction(hangar.iconAlert.Confirm, "confirm", "confirm", 1));
        }

        public void BuildArenaGui()
        {
            var consoleBackground = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
            consoleBackground.SetPixel(0, 0, new Color(1, 1, 1, 1f));
            consoleBackground.Apply(); // not sure if this is necessary

            // basically just create a copy of the "none style"
            // and then change the properties as desired
            var debugStyle = new GUIStyle(GUIStyle.none);
            debugStyle.fontSize = 12;
            debugStyle.normal.textColor = Color.black;
            debugStyle.normal.background = consoleBackground;

            //for rect in rects, draw rect
            foreach (var space in HangarSpaces)
            {
                if (space.animal != null)
                {
                    GUI.Box(new Rect(space.screenPosition.x, space.screenPosition.y, 100, 50),
                        space.animal.name + "\n" + (100*roundData.getPickRateForAnimalRound(space.animal.name, round)) +"%", debugStyle);
                }
            }

            foreach (var space in ShopSpaces)
            {
                if (space.animal != null)
                {
                    GUI.Box(new Rect(space.screenPosition.x, space.screenPosition.y, 100, 50),
                        space.animal.name + "\n" + (100*roundData.getPickRateForAnimalRound(space.animal.name, round)) + "%", debugStyle);
                }
            }

            // create a box with the queued actions
            var i = 0;
            foreach (var action in queuedActions)
            {
                GUI.Box(new Rect(0, 50 + i * 65, 200, 60), action.name + "\n" + action.info + "\n" + action.bufferTime,
                    debugStyle);
                i++;
            }
        }

        #endregion

        // MENU MODE

        #region Menu Mode

        PageManager pageManager;

        public void MenuMode()
        {
            clickArena();
        }

        public bool menuClicked = false;

        public void SetupMenu()
        {
            menuClicked = false;
            pageManager = GameObject.FindObjectOfType<PageManager>();
            if (pageManager == null)
            {
                // LoggerInstance.Msg("PageManager not found");
                return;
            }

            // LoggerInstance.Msg("found pageManager " + pageManager);
            actionTimer += Time.time + 5f;
        }

        public void clickArena()
        {
            if (menuClicked)
            {
                return;
            }

            foreach (Page page in pageManager.Pages)
            {
                if (page.name == "Lobby")
                {
                    page.GetComponent<Lobby>().ArenaButton.GetComponent<SelectableBase>().Click();
                }
            }

            menuClicked = true;
        }

        public void BuildMenuGui()
        {
        }

        #endregion

        // Battle Mode

        #region Battle Mode

        public bool fileWritten = false;


        public void SetupBattle()
        {
            // LoggerInstance.Msg("Setting up battle");
            fileWritten = false;
            Time.timeScale = 4f;
            queuedActions.Clear();
            waitAction(5);
        }

        public void BattleMode()
        {
            var uiBattle = UIBattle._instance;
            var tallyArena = uiBattle.TallyArena;
            var tallyArenaFinale = uiBattle.TallyArenaFinale;

            // var tallyArenaCanvas = tallyArena.Canvas;
            if (uiBattle != null && tallyArena != null)
            {
                uiBattle.TallyArena.Button.Click();
            }

            if (uiBattle != null && tallyArenaFinale != null)
            {
                uiBattle.TallyArenaFinale.Button.Click();
            }

            if (!fileWritten)
            {
                var serializer = CreateRoundSerializer();
                if (serializer != null)
                {
                    roundData.addRound(serializer);
                    fileWritten = true;
                }
            }
        }

        private RoundData.RoundSerializer CreateRoundSerializer()
        {
            BattleController battleController = BattleController._instance;
            if(battleController == null)
            {
                // LoggerInstance.Msg("CreateRoundSerializer battleController not found");
                return null;
            }
            // write other team to file
            // LoggerInstance.Msg("creating round serializer");
            var roundSerializer = new RoundData.RoundSerializer();
            
            if (battleController.BattleGrid == null ||battleController.BattleGrid.Model == null)
            {
                // LoggerInstance.Msg("CreateRoundSerializer BattleGrid not ready");
                return null;
            }

            try
            {
                roundSerializer.hat = battleController.BattleGrid.Model.OpponentCosmetic.ToString();
                roundSerializer.teamName = battleController._opponentTeamName;
                roundSerializer.playerName = battleController._opponentName;
            }
            catch (Exception e)
            {
                // LoggerInstance.Msg("battle grid not ready " + e.Message);
                return null;
            }
           
            roundSerializer.round = round;
            roundSerializer.outcome = battleController._outcome.ToString();
            var opponentBoardModel = battleController.OpponentBoardModel;
            var i = 0;
            foreach (var minion in opponentBoardModel.Minions.Items)
            {
                if (minion == null)
                {
                    i++;
                    continue;
                }

                var teamAnimalSerializer = new RoundData.TeamAnimalSerializer();
                teamAnimalSerializer.name = minion.Enum.ToString();
                teamAnimalSerializer.attack = minion.Attack.Total;
                teamAnimalSerializer.health = minion.Health.Total;
                teamAnimalSerializer.level = minion.Level;
                teamAnimalSerializer.position = i;
                i++;
                roundSerializer.animals.Add(teamAnimalSerializer);
            }
            return roundSerializer;
        }

        #endregion
    }
}