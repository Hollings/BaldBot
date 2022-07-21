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
using Spacewood.Unity.MonoBehaviours.Board;
using Spacewood.Unity.MonoBehaviours.Build;
using Spacewood.Unity.UI;
using Spacewood.Unity.Views;
using UnityEngine;
using Object = UnityEngine.Object;
using SpaceWoodSpace = Spacewood.Unity.Views.Space;


namespace ClassLibrary1
{
    public class MyMod : MelonMod
    {
        public string gameState = "None";
        
        // MAIN STUFF
        private float actionTimer = 3f;
        private static float defaultActionTime = 2f;
        private float nextActionTime = 2f;
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
        }
        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            LoggerInstance.Msg("Scene was initialized " + sceneName);
            
            gameState = sceneName;
            if (gameState == "Build")
            {
                SetupBuild();
            } if (gameState == "Menu")
            {
                while (pageManager == null)
                {
                    SetupMenu();
                }
            }
            if (gameState == "Battle")
            {
                SetupBattle();
            }
        }
        public override void OnUpdate()
        {
            switch (gameState) {
                case "Build":
                    BuildPhase();
                    break;
                case "Menu":
                    MenuMode();
                    break;
            }
        }

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
        private bool turnOver = false;
        List<QueuedAction> queuedActions = new List<QueuedAction>();

        public class QueuedAction
        {
            public Action action;
            public string name;
            public string info;
            public float bufferTime;
            public QueuedAction(Action action, string name, string info, float bufferTime = -1f)
            {
                if(bufferTime<0f)
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
        }
        
        public void BuildPhase()
        {
            if(HangarSpaces.Count == 0)
            {
                SetupBuild();
            }

           
            
            actionTimer += Time.deltaTime;
            if(actionTimer > nextActionTime)
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
            
            if (Input.GetKeyDown(KeyCode.Return))
            {
                chooseNextAction();
            }
        }
        
        public void doQueuedAction()
        {
            if (!queuedActions.Any()) return;
            // pop the first action off the queue and execute it
            var action = queuedActions.First();
            LoggerInstance.Msg("Doing Queued Action: " + action.action.Method.Name + " " + action.info);
            queuedActions.Remove(action);
            action.action();
            nextActionTime = action.bufferTime;
            actionTimer = 0f;
            InitializeSpaces();
        }
        
        private void SetupBuild()
        {
            turnOver = false;
            queuedActions.Clear();
            LoggerInstance.Msg("setting up");
            boardController = BoardController.Instance;
            boardView = boardController.BoardView;
            hangar = boardController.Hangar;
            minionArmy = hangar.MinionArmy;
            minionShop = hangar.MinionShop;
            hangarOverlay = hangar.Overlay;
            InitializeSpaces();
            waitAction(5);
        }
        private void InitializeSpaces()
        {
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
            foreach(var minion in minionsByPosition){
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
            foreach(var minion in shopMinions){
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
            queuedActions.Add(new QueuedAction(() => { }, "Wait", "waitAction", time));
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
            var space = HangarSpaces[position];
            space.Click();
            hangarOverlay.SellButton.Click();
        }

        public void mergeAnimal(int hangarPosition1, int hangarPosition2)
        {
            queuedActions.Add(new QueuedAction(HangarSpaces[hangarPosition1].Click, "Click", "click", 0.25f));
            queuedActions.Add(new QueuedAction(HangarSpaces[hangarPosition2].Click, "Click", "click"));
        }
        
        public void buyAnimal(int shopPosition, int hangarPosition, bool insert = false)
        {
            
            if(insert)
            {
                queuedActions.Insert(0, new QueuedAction(ShopSpaces[shopPosition].Click, "Click", "click", 0.25f));
                queuedActions.Insert(1, new QueuedAction(HangarSpaces[hangarPosition].Click, "Click", "click"));
            }
            else
            {
                queuedActions.Add(new QueuedAction(ShopSpaces[shopPosition].Click, "Click", "click", 0.25f));
                queuedActions.Add(new QueuedAction(HangarSpaces[hangarPosition].Click, "Click", "click"));
            }

        }

        public void chooseNextAction()
        {
            InitializeSpaces();
            if(currentGold() >= 3)
            {
                if (openSpace())
                {
                    buyAnimal(0,0);
                }
                else if (upgradeAvailable())
                {
                    upgradeAnimals();
                }
                else
                {
                    rollShop();
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
            foreach(var hangarspace1 in HangarSpaces)
            {
                foreach(var hangarSpace2 in HangarSpaces)
                {
                    if(hangarspace1 == hangarSpace2)
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
            foreach(var shopSpace in ShopSpaces)
            {
                foreach(var hangarSpace in HangarSpaces)
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
        
        public void upgradeAnimals()
        {
            // upgrade from hangar
            foreach(var hangarspace1 in HangarSpaces)
            {
                foreach(var hangarSpace2 in HangarSpaces)
                {
                    if(hangarspace1 == hangarSpace2)
                    {
                        continue;
                    }
                    
                    if (hangarspace1.animal != null && 
                        hangarSpace2.animal != null && 
                        hangarSpace2.animal.name == hangarspace1.animal.name)
                    {
                        mergeAnimal(hangarSpace2.index, hangarspace1.index);
                        return;
                    }
                }
            }
            
            // upgrade from shop
            foreach(var shopSpace in ShopSpaces)
            {
                foreach(var hangarSpace in HangarSpaces)
                {
                    if (shopSpace.animal != null && 
                        hangarSpace.animal != null && 
                        shopSpace.animal.name == hangarSpace.animal.name)
                    {
                        buyAnimal(shopSpace.index, hangarSpace.index, true);
                        return;
                    }
                }
            }
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
                    GUI.Box(new Rect(space.screenPosition.x, space.screenPosition.y, 100, 50), space.animal.name, debugStyle);
                }
            }
            foreach (var space in ShopSpaces)
            {
                if (space.animal != null)
                {
                    GUI.Box(new Rect(space.screenPosition.x, space.screenPosition.y, 100, 50), space.animal.name, debugStyle);
                }
            }
            
            // create a box with the queued actions
            GUI.Box(new Rect(0, 0, 200, 50), "Queued Actions");
            var i = 0;
            foreach (var action in queuedActions)
            {
                GUI.Box(new Rect(0, 50 + i * 65, 200, 60), action.name + "\n" + action.info + "\n" + action.bufferTime, debugStyle);
                i++;
            }
        }
        #endregion
        
        // MENU MODE
        #region Menu Mode
        PageManager pageManager;

        public void MenuMode()
        {
            // clickArena();
        }
        
        public void SetupMenu()
        {
            pageManager = GameObject.FindObjectOfType<PageManager>();
            if(pageManager == null)
            {
                LoggerInstance.Msg("PageManager not found");
                return;
            }
            LoggerInstance.Msg("found pageManager " + pageManager);
            actionTimer += Time.time + 5f;
        }
        
        public void clickContinue()
        {
            foreach (Page page in pageManager.Pages)
            {
                if (page.name == "Lobby")
                {
                    page.GetComponent<Lobby>().playBox.Button.Click();
                }
            }
        }
        public void clickArena()
        {
            foreach (Page page in pageManager.Pages)
            {
                if (page.name == "Lobby")
                {
                    page.GetComponent<Lobby>().ArenaButton.GetComponent<SelectableBase>().Click();
                }
            }
        }

        public void BuildMenuGui()
        {

        }
        #endregion
        
        // Battle Mode
        #region Battle Mode

        public void SetupBattle()
        {
            queuedActions.Clear();
        }
        #endregion
    }
    
   
}
