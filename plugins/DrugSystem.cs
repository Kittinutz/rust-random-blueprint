using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Drug System", "apolocity", "1.0.5")]
    public class DrugSystem : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin ImageLibrary;

        private const string Layer = "UI.DrugSystem";

        private const string TableLayer = "UI.DrugSystem.Table";

        private const string CocaineLayer = "UI.DrugSystem.Cocaine";

        private const string BotLayer = "UI.DrugSystem.Bot";

        private static DrugSystem _instance;

        private readonly List<TableComponent> _tables = new List<TableComponent>();

        private readonly Dictionary<BasePlayer, TableComponent> _tableByPlayer =
            new Dictionary<BasePlayer, TableComponent>();

        private readonly Dictionary<BasePlayer, BotComponent>
            _botComponents = new Dictionary<BasePlayer, BotComponent>();

        #endregion

        #region Config

        private static Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Permission")]
            public string Permission = "drugsystem.use";

            [JsonProperty(PropertyName = "Receipts", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<Receipt> Receipts = new List<Receipt>
            {
                new Receipt
                {
                    Items = new List<ItemConf>
                    {
                        new ItemConf("red.berry", 2555182455, "Poppy Flower", 1, "https://i.imgur.com/sRYHpuT.png"),
                        new ItemConf("bleach", 0, string.Empty),
                        new ItemConf("antiradpills", 0, string.Empty)
                    },
                    MixTime = 15,
                    CommandToGive = string.Empty,
                    CommandToGiveIngredients = "give.ingredients.cocaine",
                    ResultItem = new ItemConf("glue", 2553143857, "Cocaine", 1, "https://i.imgur.com/d83phHA.png"),
                    Description = new List<string>
                    {
                        "string 1",
                        "string 2",
                        "string 3"
                    },
                    MainDescription = "Cocaine Description"
                },
                new Receipt
                {
                    Items = new List<ItemConf>
                    {
                        new ItemConf("seed.hemp", 2555171480, "Cannabis Leaves", 1, "https://i.imgur.com/0doAEF7.png"),
                        new ItemConf("knife.butcher", 0, string.Empty)
                    },
                    MixTime = 15,
                    CommandToGive = string.Empty,
                    CommandToGiveIngredients = "give.ingredients.weed",
                    ResultItem = new ItemConf("coal", 2555171111, "Weed", 1, "https://i.imgur.com/Fmqjrqp.png"),
                    Description = new List<string>
                    {
                        "string 1",
                        "string 2",
                        "string 3"
                    },
                    MainDescription = "Weed Description"
                }
            };

            [JsonProperty(PropertyName = "Bonk Settings")]
            public CustomItemConf Bonk = new CustomItemConf("battery.small", 2555171325, "Bonk", "give.bonk");

            [JsonProperty(PropertyName = "Weed Settings")]
            public CustomItemConf Weed = new CustomItemConf("coal", 2555171111, "Weed", "give.weed");

            [JsonProperty(PropertyName = "Bonk + Weed Effects")]
            public EffectsConf BonkWeedEffects = new EffectsConf
            {
                Health = 0,
                Hydration = 800,
                Calories = 800
            };

            [JsonProperty(PropertyName = "Cocaine Settings")]
            public CocaineConf Cocaine = new CocaineConf("blood", 2553143857, "Cocaine", "give.cocaine", 5);

            [JsonProperty(PropertyName = "Cocaine Effects")]
            public EffectsConf CocaineEffects = new EffectsConf
            {
                Health = 100,
                Hydration = 800,
                Calories = 800
            };

            [JsonProperty(PropertyName = "Bots for processing",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> BoTs = new List<string>
            {
                "1234567",
                "7654321",
                "4644687478"
            };

            [JsonProperty(PropertyName = "Max Bot Distance")]
            public float MaxBotDistance = 1f;
        }

        private class EffectsConf
        {
            [JsonProperty(PropertyName = "Health")]
            public float Health;

            [JsonProperty(PropertyName = "Hydration")]
            public float Hydration;

            [JsonProperty(PropertyName = "Calories")]
            public float Calories;

            public void Get(BasePlayer player)
            {
                player.metabolism.hydration.max = Hydration;
                player.metabolism.hydration.SetValue(Hydration);
                player.metabolism.calories.max = Calories;
                player.metabolism.calories.SetValue(Calories);

                if (Health > player.MaxHealth())
                    player.SetMaxHealth(Health);

                player.Heal(Health);

                var guid = CuiHelper.GetGuid();
                var container = new CuiElementContainer
                {
                    new CuiElement
                    {
                        Name = guid,
                        Parent = "Overlay",
                        Components =
                        {
                            new CuiRawImageComponent { Sprite = "assets/content/ui/overlay_bleeding.png" },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                        }
                    }
                };

                CuiHelper.AddUi(player, container);

                player.Invoke(() => CuiHelper.DestroyUi(player, guid), 5f);
            }
        }

        private class CocaineConf : CustomItemConf
        {
            [JsonProperty(PropertyName = "Using Cooldown")]
            public float Cooldown;

            public CocaineConf(string shortName, ulong skin, string displayName, string cmd, float cooldown) : base(
                shortName, skin, displayName, cmd)
            {
                Cooldown = cooldown;
            }
        }

        private class CustomItemConf : ItemConf
        {
            [JsonProperty(PropertyName = "Command to give")]
            public string CommandToGive;

            public CustomItemConf(string shortName, ulong skin, string displayName, string cmd) : base(shortName, skin,
                displayName)
            {
                CommandToGive = cmd;
            }
        }

        private class Receipt
        {
            [JsonProperty(PropertyName = "Items to Process", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ItemConf> Items = new List<ItemConf>();

            [JsonProperty(PropertyName = "Mix Time")]
            public float MixTime;

            [JsonProperty(PropertyName = "Result Item")]
            public ItemConf ResultItem;

            [JsonProperty(PropertyName = "Command to give")]
            public string CommandToGive;

            [JsonProperty(PropertyName = "Command to give ingredients")]
            public string CommandToGiveIngredients;

            [JsonProperty(PropertyName = "Main Description")]
            public string MainDescription;

            [JsonProperty(PropertyName = "Description", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Description;
        }

        private class ItemConf
        {
            [JsonProperty(PropertyName = "Image")] public string Image;

            [JsonProperty(PropertyName = "ShortName")]
            public string ShortName;

            [JsonProperty(PropertyName = "Skin")] public ulong Skin;

            [JsonProperty(PropertyName = "Display Name (empty - default)")]
            public string DisplayName;

            [JsonProperty(PropertyName = "Amount")] [DefaultValue(1)]
            public int Amount;

            [JsonIgnore] private string _publicTitle;

            [JsonIgnore]
            public string PublicTitle
            {
                get
                {
                    if (string.IsNullOrEmpty(_publicTitle))
                        _publicTitle = GetName();

                    return _publicTitle;
                }
            }

            private string GetName()
            {
                if (!string.IsNullOrEmpty(DisplayName))
                    return DisplayName;

                var def = ItemManager.FindItemDefinition(ShortName);
                if (!string.IsNullOrEmpty(ShortName) && def != null)
                    return def.displayName.translated;

                return string.Empty;
            }

            public void Take(IEnumerable<Item> itemList)
            {
                var num1 = 0;
                if (Amount == 0) return;

                var list = Pool.GetList<Item>();

                foreach (var item in itemList)
                {
                    if (item.info.shortname != ShortName ||
                        Skin != 0 && item.skin != Skin || item.isBroken) continue;

                    var num2 = Amount - num1;
                    if (num2 <= 0) continue;
                    if (item.amount > num2)
                    {
                        item.MarkDirty();
                        item.amount -= num2;
                        num1 += num2;
                        break;
                    }

                    if (item.amount <= num2)
                    {
                        num1 += item.amount;
                        list.Add(item);
                    }

                    if (num1 == Amount)
                        break;
                }

                foreach (var obj in list)
                    obj.RemoveFromContainer();

                Pool.FreeList(ref list);
            }

            public bool HasAmount(BasePlayer player)
            {
                return ItemCount(player.inventory.AllItems()) >= Amount;
            }

            public bool HasAmount(Item[] items)
            {
                return ItemCount(items) >= Amount;
            }

            public int ItemCount(BasePlayer player)
            {
                return ItemCount(player.inventory.AllItems());
            }

            private int ItemCount(Item[] items)
            {
                return items.Where(item =>
                        item.info.shortname == ShortName && !item.isBroken && (Skin == 0 || item.skin == Skin))
                    .Sum(item => item.amount);
            }

            public bool IsSame(Item item)
            {
                return item != null && item.info.shortname == ShortName && item.skin == Skin;
            }

            public Item ToItem(int amount = 1)
            {
                var item = ItemManager.CreateByName(ShortName, amount, Skin);
                if (item == null)
                {
                    Debug.LogError($"Error creating item with shortName: '{ShortName}'");
                    return null;
                }

                if (!string.IsNullOrEmpty(DisplayName)) item.name = DisplayName;

                return item;
            }

            [JsonConstructor]
            public ItemConf()
            {
            }

            public ItemConf(string shortName, ulong skin, string displayName, int amount = 1, string image = "")
            {
                ShortName = shortName;
                Skin = skin;
                DisplayName = displayName;
                Amount = amount;
                Image = image;
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
        }

        #endregion

        #region Hooks

        private void OnServerInitialized(bool initial)
        {
            _instance = this;

            LoadImages();

            _config.Receipts.ForEach(receipt =>
                AddCovalenceCommand(new[] { receipt.CommandToGive, receipt.CommandToGiveIngredients },
                    nameof(CmdReceipt)));

            if (!string.IsNullOrEmpty(_config.Permission) && !permission.PermissionExists(_config.Permission))
                permission.RegisterPermission(_config.Permission, this);

            AddCovalenceCommand(_config.Bonk.CommandToGive, nameof(CmdReceipt));
            AddCovalenceCommand(_config.Weed.CommandToGive, nameof(CmdReceipt));
            AddCovalenceCommand(_config.Cocaine.CommandToGive, nameof(CmdReceipt));

            AddCovalenceCommand(new[] { "ds.bot.add", "ds.bot.remove", "ds.bot.clear" }, nameof(CmdBots));

            if (!initial)
                foreach (var table in BaseNetworkable.serverEntities.OfType<MixingTable>())
                    OnEntitySpawned(table);
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer);
                CuiHelper.DestroyUi(player, TableLayer);
                CuiHelper.DestroyUi(player, BotLayer);
                CuiHelper.DestroyUi(player, BotLayer + ".Processing");
            }

            _tables.ToList().ForEach(table =>
            {
                if (table != null)
                    table.Kill();
            });

            _cocaineByPlayer.Values.ToList().ForEach(cocaine =>
            {
                if (cocaine != null)
                    cocaine.Kill();
            });

            _botComponents.Values.ToList().ForEach(bot =>
            {
                if (bot != null)
                    bot.Kill();
            });

            _instance = null;
            _config = null;
        }

        private void CanLootEntity(BasePlayer player, MixingTable table)
        {
            if (player == null || table == null || !string.IsNullOrEmpty(_config.Permission) &&
                !permission.UserHasPermission(player.UserIDString, _config.Permission)) return;

            TableUi(player);

            var component = table.GetComponent<TableComponent>();
            if (component != null)
                _tableByPlayer[player] = component;
        }

        private void OnLootEntityEnd(BasePlayer player, MixingTable table)
        {
            if (player == null || table == null) return;

            _tableByPlayer.Remove(player);
            CuiHelper.DestroyUi(player, TableLayer);
        }

        private void OnEntitySpawned(MixingTable table)
        {
            if (table == null) return;

            table.gameObject.AddComponent<TableComponent>();
        }

        private bool? CanStackItem(Item item, Item targetItem)
        {
            if (item == null || targetItem == null) return null;

            var player = item.GetOwnerPlayer() ?? targetItem.GetOwnerPlayer();
            if (player == null) return null;

            if (_config.Bonk.IsSame(item) && _config.Weed.IsSame(targetItem))
            {
                targetItem.Remove();
                _config.BonkWeedEffects.Get(player);

                ItemManager.DoRemoves();
                return false;
            }

            return null;
        }

        private void OnMixingTableToggle(MixingTable table, BasePlayer player)
        {
            if (table == null || player == null) return;

            var component = table.GetComponent<TableComponent>();
            if (component == null) return;

            if (table.IsOn())
                component.StopMixing();
        }

        private object OnItemPickup(Item item, BasePlayer player)
        {
            if (item == null || player == null) return null;

            if (!_config.Cocaine.IsSame(item)) return null;

            NextTick(() =>
            {
                if (player.serverInput.IsDown(BUTTON.USE))
                    StartCocaine(item, player);
                else
                    Pickup(item, player);
            });

            return true;
        }

        private void Pickup(Item item, BasePlayer player)
        {
            player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
            player.SignalBroadcast(BaseEntity.Signal.Gesture, "pickup_item");
        }

        private void OnUseNPC(BasePlayer npc, BasePlayer player)
        {
            if (player == null || npc == null || !_config.BoTs.Contains(npc.UserIDString)) return;

            if (string.IsNullOrEmpty(_config.Permission) ||
                permission.UserHasPermission(player.UserIDString, _config.Permission))
            {
                Reply(player, NoPermission);
                return;
            }

            var botComponent = GetBotComponent(player);
            if (botComponent != null)
                botComponent.Kill();

            player.gameObject.AddComponent<BotComponent>().Init(npc.ServerPosition);
        }

        #endregion

        #region Commands

        private void CmdReceipt(IPlayer cov, string command, string[] args)
        {
            if (!cov.IsAdmin) return;

            if (args.Length == 0)
            {
                PrintError($"Error syntax! Use: /{command} [targetId]");
                return;
            }

            var target = covalence.Players.FindPlayer(args[0])?.Object as BasePlayer;
            if (target == null)
            {
                PrintError($"Player '{args[0]}' not found!");
                return;
            }

            if (_config.Bonk.CommandToGive == command)
            {
                var item = _config.Bonk.ToItem();
                if (item != null)
                    target.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
                return;
            }

            if (_config.Weed.CommandToGive == command)
            {
                var item = _config.Weed.ToItem();
                if (item != null)
                    target.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
                return;
            }

            if (_config.Cocaine.CommandToGive == command)
            {
                var item = _config.Cocaine.ToItem();
                if (item != null)
                    target.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
                return;
            }

            var receipt = _config.Receipts.Find(x => x.CommandToGive == command);
            if (receipt != null)
            {
                var item = receipt.ResultItem.ToItem();
                if (item != null)
                    target.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
                return;
            }

            receipt = _config.Receipts.Find(x => x.CommandToGiveIngredients == command);
            receipt?.Items.ForEach(check =>
            {
                var item = check.ToItem();
                if (item != null)
                    target.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
            });
        }

        private void CmdBots(IPlayer cov, string command, string[] args)
        {
            if (!cov.IsAdmin) return;

            switch (command)
            {
                case "ds.bot.add":
                {
                    if (args.Length < 1)
                    {
                        cov.Reply($"Error syntax! Use: /{command} [botId]");
                        return;
                    }

                    var botId = string.Join(" ", args);
                    if (string.IsNullOrEmpty(botId))
                        return;

                    if (!_config.BoTs.Contains(botId))
                        _config.BoTs.Add(botId);

                    Puts($"Bot '{botId}' added to config!");
                    SaveConfig();
                    break;
                }
                case "ds.bot.remove":
                {
                    if (args.Length < 1)
                    {
                        cov.Reply($"Error syntax! Use: /{command} [botId]");
                        return;
                    }

                    var botId = string.Join(" ", args);
                    if (string.IsNullOrEmpty(botId))
                        return;

                    _config.BoTs.Remove(botId);

                    Puts($"Bot '{botId}' removed from config!");
                    SaveConfig();
                    break;
                }
                case "ds.bot.clear":
                {
                    _config.BoTs.Clear();

                    Puts("Bots cleared from config!");
                    SaveConfig();
                    break;
                }
            }
        }

        [ConsoleCommand("UI_Drugs")]
        private void CmdConsoleDrugs(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null || !arg.HasArgs()) return;

            switch (arg.Args[0])
            {
                case "start":
                {
                    int id;
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out id)) return;

                    TableComponent table;
                    if (!_tableByPlayer.TryGetValue(player, out table) || table == null) return;

                    table.StartProcess(id);
                    break;
                }

                case "page":
                {
                    int page;
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out page)) return;

                    TableUi(player, page);
                    break;
                }

                case "info":
                {
                    int id;
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out id) || id < 0 ||
                        _config.Receipts.Count <= id) return;

                    var receipt = _config.Receipts[id];
                    if (receipt == null) return;

                    InfoUi(player, receipt);
                    break;
                }

                case "bot_page":
                {
                    int page;
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out page)) return;

                    var bot = GetBotComponent(player);
                    if (bot == null) return;

                    bot.MainUi(page);
                    break;
                }

                case "bot_start":
                {
                    int id;
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out id)) return;

                    var bot = GetBotComponent(player);
                    if (bot == null) return;

                    bot.Process(id);
                    break;
                }

                case "bot_info":
                {
                    int id;
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out id) || id < 0 ||
                        _config.Receipts.Count <= id) return;

                    var receipt = _config.Receipts[id];
                    if (receipt == null) return;

                    InfoUi(player, receipt);
                    break;
                }
            }
        }

        #endregion

        #region Interface

        private void TableUi(BasePlayer player, int recId = 0)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0", AnchorMax = "0.5 0"
                },
                Image = { Color = "0 0 0 0" }
            }, "Overlay", TableLayer);

            #region Recipe Header

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "-199.5 615", OffsetMax = "180 635"
                },
                Image =
                {
                    Color = HexToCuiColor("#595651", 85),
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                }
            }, TableLayer, TableLayer + ".Header.Recipe");

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "5 0", OffsetMax = "-5 0"
                },
                Text =
                {
                    Text = Msg(player, RecipeName),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 14,
                    Color = "1 1 1 0.55"
                }
            }, TableLayer + ".Header.Recipe");

            #endregion

            #region Recipes

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "-199.5 510", OffsetMax = "180 610"
                },
                Image =
                {
                    Color = HexToCuiColor("#595651", 70),
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                }
            }, TableLayer, TableLayer + ".Recipes");

            var ItemSize = 85f;
            var margin = 5f;
            var xSwitch = -(_config.Receipts.Count * ItemSize + (_config.Receipts.Count - 1) * margin) / 2f;

            for (var i = 0; i < _config.Receipts.Count; i++)
            {
                var receipt = _config.Receipts[i];

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                        OffsetMin = $"{xSwitch} {-ItemSize / 2f}",
                        OffsetMax = $"{xSwitch + ItemSize} {ItemSize / 2f}"
                    },
                    Image =
                    {
                        Color = recId == i ? HexToCuiColor("#46C1FC", 35) : HexToCuiColor("#595651", 70)
                    }
                }, TableLayer + ".Recipes", TableLayer + $".Recipe.{i}");

                container.Add(new CuiElement
                {
                    Parent = TableLayer + $".Recipe.{i}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = !string.IsNullOrEmpty(receipt.ResultItem.Image)
                                ? _instance.ImageLibrary.Call<string>("GetImage", receipt.ResultItem.Image)
                                : _instance.ImageLibrary.Call<string>("GetImage", receipt.ResultItem.ShortName,
                                    receipt.ResultItem.Skin)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1",
                            OffsetMin = "5 5", OffsetMax = "-5 -5"
                        }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Text = { Text = "" },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = $"UI_Drugs page {i}"
                    }
                }, TableLayer + $".Recipe.{i}");

                xSwitch += ItemSize + margin;
            }

            #endregion

            #region Ingredients Title

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "-199.5 490", OffsetMax = "180 505"
                },
                Image =
                {
                    Color = HexToCuiColor("#595651", 85),
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                }
            }, TableLayer, TableLayer + ".Header.Ingredients");

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "5 0", OffsetMax = "-5 0"
                },
                Text =
                {
                    Text = Msg(player, IngredientsTitle),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 14,
                    Color = "1 1 1 0.55"
                }
            }, TableLayer + ".Header.Ingredients");

            #endregion

            #region Ingredients

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "-199.5 365", OffsetMax = "180 485"
                },
                Image =
                {
                    Color = HexToCuiColor("#595651", 70),
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                }
            }, TableLayer, TableLayer + ".Ingredients");

            var Height = 20f;
            margin = 5f;

            var ySwitch = -35f;

            if (recId >= 0 && _config.Receipts.Count > recId)
            {
                var receipt = _config.Receipts[recId];

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "10 0", OffsetMax = "0 -5"
                    },
                    Text =
                    {
                        Text = $"{receipt.MainDescription}",
                        Align = TextAnchor.UpperLeft,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 12,
                        Color = "1 1 1 0.6"
                    }
                }, TableLayer + ".Ingredients");

                #region Table

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = "10 -35", OffsetMax = "40 0"
                    },
                    Text =
                    {
                        Text = Msg(player, NeedTitle),
                        Align = TextAnchor.LowerCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 0.5"
                    }
                }, TableLayer + ".Ingredients");

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = "50 -35", OffsetMax = "200 0"
                    },
                    Text =
                    {
                        Text = Msg(player, ItemTitle),
                        Align = TextAnchor.LowerLeft,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 0.5"
                    }
                }, TableLayer + ".Ingredients");

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = "175 -35", OffsetMax = "225 0"
                    },
                    Text =
                    {
                        Text = Msg(player, HaveTitle),
                        Align = TextAnchor.LowerCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 0.5"
                    }
                }, TableLayer + ".Ingredients");

                #endregion

                for (var i = 0; i < receipt.Items.Count; i++)
                {
                    var item = receipt.Items[i];

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"0 {ySwitch - Height}",
                            OffsetMax = $"0 {ySwitch}"
                        },
                        Image = { Color = "0 0 0 0" }
                    }, TableLayer + ".Ingredients", TableLayer + $".Ingredient.{i}");

                    #region Amount

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1",
                            OffsetMin = "10 0", OffsetMax = "40 0"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#000000", 40)
                        }
                    }, TableLayer + $".Ingredient.{i}", TableLayer + $".Ingredient.{i}.Amount");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Text =
                        {
                            Text = $"{item.Amount}",
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 12,
                            Color = "1 1 1 0.55"
                        }
                    }, TableLayer + $".Ingredient.{i}.Amount");

                    #endregion

                    #region Title

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1",
                            OffsetMin = "45 0", OffsetMax = "170 0"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#000000", 40)
                        }
                    }, TableLayer + $".Ingredient.{i}", TableLayer + $".Ingredient.{i}.Title");

                    container.Add(new CuiElement
                    {
                        Parent = TableLayer + $".Ingredient.{i}.Title",
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = !string.IsNullOrEmpty(item.Image)
                                    ? _instance.ImageLibrary.Call<string>("GetImage", item.Image)
                                    : _instance.ImageLibrary.Call<string>("GetImage", item.ShortName, item.Skin)
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "0 0",
                                OffsetMin = "2 2", OffsetMax = "18 18"
                            }
                        }
                    });

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1",
                            OffsetMin = "20 0", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = $"{item.PublicTitle}",
                            Align = TextAnchor.MiddleLeft,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 12,
                            Color = "1 1 1 0.55"
                        }
                    }, TableLayer + $".Ingredient.{i}.Title");

                    #endregion

                    #region Has Amount

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1",
                            OffsetMin = "175 0", OffsetMax = "225 0"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#000000", 40)
                        }
                    }, TableLayer + $".Ingredient.{i}", TableLayer + $".Ingredient.{i}.HasAmount");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Text =
                        {
                            Text = Msg(player, HasAmountTitle, item.ItemCount(player)),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 12,
                            Color = "1 1 1 0.55"
                        }
                    }, TableLayer + $".Ingredient.{i}.HasAmount");

                    #endregion

                    ySwitch = ySwitch - Height - margin;
                }
            }

            #endregion

            #region Buttons

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = "-145 -80", OffsetMax = "-5 -35"
                },
                Text =
                {
                    Text = Msg(player, ProcessBtn),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 18,
                    Color = "1 1 1 0.55"
                },
                Button =
                {
                    Color = HexToCuiColor("#6EA642", 70),
                    Command = $"UI_Drugs start {recId}"
                }
            }, TableLayer + ".Ingredients");

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = "-145 -105", OffsetMax = "-5 -85"
                },
                Text =
                {
                    Text = Msg(player, InfoBtn),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 11,
                    Color = "1 1 1 0.6"
                },
                Button =
                {
                    Color = HexToCuiColor("#000000", 40),
                    Command = $"UI_Drugs info {recId}"
                }
            }, TableLayer + ".Ingredients");

            #endregion

            CuiHelper.DestroyUi(player, TableLayer);
            CuiHelper.AddUi(player, container);
        }

        /*private void TableUi(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                    OffsetMin = "202 54", OffsetMax = "302 79"
                },
                Text =
                {
                    Text = "PROCESS",
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 12,
                    Color = "1 1 1 0.35"
                },
                Button =
                {
                    Color = HexToCuiColor("#8CC83C", 50),
                    Command = "UI_Drugs start"
                }
            }, "Overlay", TableLayer);

            CuiHelper.DestroyUi(player, TableLayer);
            CuiHelper.AddUi(player, container);
        }*/

        private void InfoUi(BasePlayer player, Receipt receipt)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "192 510", OffsetMax = "575 635"
                },
                Image =
                {
                    Color = HexToCuiColor("#595651", 85),
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                }
            }, TableLayer, TableLayer + ".Description");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text =
                {
                    Text = $"{string.Join("\n", receipt.Description)}",
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 14,
                    Color = "1 1 1 0.95"
                }
            }, TableLayer + ".Description");

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = "-25 -25", OffsetMax = "0 0"
                },
                Text =
                {
                    Text = "X",
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 20,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = "0 0 0 0",
                    Close = TableLayer + ".Description"
                }
            }, TableLayer + ".Description");

            CuiHelper.DestroyUi(player, TableLayer + ".Description");
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Utils

        private static string HexToCuiColor(string hex, float alpha = 100)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#FFFFFF";

            var str = hex.Trim('#');
            if (str.Length != 6) throw new Exception(hex);
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {alpha / 100}";
        }

        private BotComponent GetBotComponent(BasePlayer player)
        {
            BotComponent bot;
            return _botComponents.TryGetValue(player, out bot) && bot != null ? bot : null;
        }

        private void LoadImages()
        {
            if (!ImageLibrary)
            {
                PrintWarning("IMAGE LIBRARY IS NOT INSTALLED!");
            }
            else
            {
                var imagesList = new Dictionary<string, string>();

                var itemIcons = new List<KeyValuePair<string, ulong>>();

                _config.Receipts.ForEach(receipt =>
                {
                    if (!string.IsNullOrEmpty(receipt.ResultItem.Image) &&
                        !imagesList.ContainsKey(receipt.ResultItem.Image))
                        imagesList.Add(receipt.ResultItem.Image, receipt.ResultItem.Image);

                    itemIcons.Add(
                        new KeyValuePair<string, ulong>(receipt.ResultItem.ShortName, receipt.ResultItem.Skin));

                    receipt.Items.ForEach(item =>
                    {
                        if (!string.IsNullOrEmpty(item.Image) && !imagesList.ContainsKey(item.Image))
                            imagesList.Add(item.Image, item.Image);

                        itemIcons.Add(new KeyValuePair<string, ulong>(item.ShortName, item.Skin));
                    });
                });

                if (itemIcons.Count > 0) ImageLibrary?.Call("LoadImageList", Title, itemIcons, null);

                ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
            }
        }

        #endregion

        #region Component

        private class TableComponent : FacepunchBehaviour
        {
            private MixingTable _table;

            private bool _started;

            private Receipt _currentRecipe;

            #region Init

            private void Awake()
            {
                _table = GetComponent<MixingTable>();

                _table.OnlyAcceptValidIngredients = false;

                _instance?._tables.Add(this);
            }

            #endregion

            #region Main

            public void StartProcess(int recId)
            {
                if (_table.IsOn())
                    return;

                var items = _table.inventory.itemList.ToArray();

                var receipt = _config.Receipts[recId];
                if (receipt == null || receipt.Items.Any(x => !x.HasAmount(items))) return;

                _currentRecipe = receipt;

                _table.RemainingMixTime = receipt.MixTime;
                _table.TotalMixTime = receipt.MixTime;

                InvokeRepeating(TickMix, 1f, 1f);
                _table.SetFlag(BaseEntity.Flags.On, true);
                _table.SendNetworkUpdateImmediate();

                _started = true;
            }

            public void TickMix()
            {
                _table.lastTickTimestamp = Time.realtimeSinceStartup;
                --_table.RemainingMixTime;
                _table.SendNetworkUpdateImmediate();
                if (_table.RemainingMixTime > 0.0)
                    return;
                ProduceItem(_currentRecipe);
            }

            public void ProduceItem(Receipt receipt)
            {
                ConsumeInventory(receipt);

                CreateRecipeItems(receipt);

                StopMixing();
            }

            private void ConsumeInventory(Receipt receipt)
            {
                receipt.Items.ForEach(check => check.Take(_table.inventory.itemList));

                ItemManager.DoRemoves();
            }

            private void CreateRecipeItems(Receipt receipt)
            {
                var result = receipt.ResultItem.ToItem();
                if (result == null) return;

                if (!result.MoveToContainer(_table.inventory))
                    result.Drop(_table.inventory.dropPosition, _table.inventory.dropVelocity);
            }

            public void StopMixing()
            {
                _currentRecipe = null;

                _table.currentQuantity = 0;
                _table.RemainingMixTime = 0.0f;

                CancelInvoke(TickMix);

                if (!_table.IsOn()) return;

                _table.SetFlag(BaseEntity.Flags.On, false);
                _table.SendNetworkUpdateImmediate();

                _started = false;
            }

            #endregion

            #region Destroy

            private void OnDestroy()
            {
                CancelInvoke();

                if (_started)
                    StopMixing();

                _instance?._tables.Remove(this);

                Destroy(this);
            }

            public void Kill()
            {
                DestroyImmediate(this);
            }

            #endregion
        }

        #endregion

        #region Cocaine Use

        private readonly Dictionary<BasePlayer, CocaineComponent> _cocaineByPlayer =
            new Dictionary<BasePlayer, CocaineComponent>();

        private void StartCocaine(Item item, BasePlayer player)
        {
            if (_cocaineByPlayer.ContainsKey(player)) _cocaineByPlayer[player].Kill();

            player.gameObject.AddComponent<CocaineComponent>().Init(item);
        }

        private class CocaineComponent : FacepunchBehaviour
        {
            private BasePlayer _player;

            private Item _item;

            private const float Cooldown = 5f;

            private float _startTime;

            private bool _started;

            private void Awake()
            {
                _player = GetComponent<BasePlayer>();

                _instance._cocaineByPlayer[_player] = this;
            }

            public void Init(Item item)
            {
                _item = item;

                _startTime = Time.time;

                _started = true;

                enabled = true;

                ProcessUi();
            }

            public void Stop(bool crash = false)
            {
                _started = false;

                enabled = false;

                if (_player != null)
                    CuiHelper.DestroyUi(_player, CocaineLayer);

                if (!crash)
                {
                    _item?.Remove();
                    ItemManager.DoRemoves();

                    if (_player != null)
                        _config.CocaineEffects.Get(_player);
                }

                Kill();
            }

            private void FixedUpdate()
            {
                ProcessUi();
            }

            private void ProcessUi()
            {
                if (_item == null || _item.GetOwnerPlayer() != null || !_player.serverInput.IsDown(BUTTON.USE))
                {
                    Stop(true);
                    return;
                }

                var timeLeft = Time.time - _startTime;
                if (timeLeft > Cooldown)
                {
                    Stop();
                    return;
                }

                var container = new CuiElementContainer
                {
                    {
                        new CuiPanel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                            Image =
                            {
                                Color = "0 0 0 0.1",
                                Material = "assets/content/ui/uibackgroundblur.mat"
                            }
                        },
                        "Overlay", CocaineLayer
                    },
                    {
                        new CuiPanel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0.5 0.5",
                                AnchorMax = "0.5 0.5",
                                OffsetMin = "-60 0",
                                OffsetMax = "60 10"
                            },
                            Image = { Color = "0 0 0 0.45" }
                        },
                        CocaineLayer, CocaineLayer + ".Progress"
                    },
                    {
                        new CuiPanel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = $"{timeLeft / Cooldown} 0.95" },
                            Image = { Color = "0.27 0.788 0.58 0.85" }
                        },
                        CocaineLayer + ".Progress"
                    },
                    {
                        new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                            Text =
                            {
                                Text = $"{Mathf.RoundToInt(Cooldown - timeLeft)} sec",
                                Align = TextAnchor.MiddleCenter,
                                FontSize = 8,
                                Font = "robotocondensed-regular.ttf",
                                Color = "1 1 1 0.99"
                            }
                        },
                        CocaineLayer + ".Progress"
                    },
                    {
                        new CuiLabel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 15"
                            },
                            Text =
                            {
                                Text = "Sniffing...",
                                Align = TextAnchor.MiddleCenter,
                                Font = "robotocondensed-bold.ttf",
                                FontSize = 12,
                                Color = "1 1 1 1"
                            }
                        },
                        CocaineLayer + ".Progress"
                    }
                };

                CuiHelper.DestroyUi(_player, CocaineLayer);
                CuiHelper.AddUi(_player, container);
            }

            private void OnDestroy()
            {
                if (_started)
                    Stop(true);

                _instance?._cocaineByPlayer.Remove(_player);

                Destroy(this);
            }

            public void Kill()
            {
                DestroyImmediate(this);
            }
        }

        #endregion

        #region Bot Component

        private class BotComponent : FacepunchBehaviour
        {
            private BasePlayer _player;

            private float _startTime;
            private float _cooldown;
            private bool _started;

            private Receipt _currentReceipt;
            private Vector3 _botPosition;

            private void Awake()
            {
                _player = GetComponent<BasePlayer>();

                _instance._botComponents[_player] = this;
            }

            public void Init(Vector3 bot)
            {
                _botPosition = bot;

                Invoke(() =>
                {
                    OpenInventory();

                    MainUi();
                }, 0.5f);
            }

            public void Process(int id)
            {
                if (id < 0 || _config.Receipts.Count <= id) return;

                var receipt = _config.Receipts[id];
                if (receipt == null || receipt.Items.Any(x => !x.HasAmount(_player))) return;

                _started = true;
                _currentReceipt = receipt;
                _startTime = Time.time;
                _cooldown = receipt.MixTime;
            }

            private void Finish()
            {
                _started = false;

                CuiHelper.DestroyUi(_player, BotLayer);
                CuiHelper.DestroyUi(_player, BotLayer + ".Processing");

                if (_currentReceipt != null)
                {
                    var items = _player.inventory.AllItems();
                    _currentReceipt.Items.ForEach(x => x.Take(items));

                    var item = _currentReceipt.ResultItem?.ToItem();
                    if (item != null)
                        _player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
                }

                Kill();
            }

            private void FixedUpdate()
            {
                if (_player == null)
                {
                    Kill();
                    return;
                }

                if (_player.Distance(_botPosition) > _config.MaxBotDistance)
                {
                    Kill();
                    return;
                }

                OpenInventory();

                if (_started)
                {
                    var timeLeft = Time.time - _startTime;
                    if (timeLeft >= _cooldown)
                    {
                        Finish();
                        return;
                    }

                    ProcessingUi();
                }
            }

            private void OpenInventory()
            {
                var loot = _player.inventory.loot;

                loot.Clear();
                loot.PositionChecks = false;
                loot.entitySource = _player;
                loot.itemSource = null;
                loot.AddContainer(null);
                loot.SendImmediate();

                _player.ClientRPCPlayer(null, _player, "RPC_OpenLootPanel", "generic");
            }

            #region Interface

            public void MainUi(int recId = 0)
            {
                var container = new CuiElementContainer();

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0", AnchorMax = "0.5 0"
                    },
                    Image = { Color = "0 0 0 0" }
                }, "Overlay", BotLayer);

                #region Title

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "192 493", OffsetMax = "300 519"
                    },
                    Text =
                    {
                        Text = _instance.Msg(_player, UiTitle),
                        Align = TextAnchor.MiddleLeft,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 18,
                        Color = "1 1 1 0.65"
                    }
                }, BotLayer);

                #endregion

                #region Recipe Header

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "192 473", OffsetMax = "572 493"
                    },
                    Image =
                    {
                        Color = HexToCuiColor("#595651", 85),
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    }
                }, BotLayer, BotLayer + ".Header.Recipe");

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "5 0", OffsetMax = "-5 0"
                    },
                    Text =
                    {
                        Text = _instance.Msg(_player, RecipeName),
                        Align = TextAnchor.MiddleLeft,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 14,
                        Color = "1 1 1 0.55"
                    }
                }, BotLayer + ".Header.Recipe");

                #endregion

                #region Recipes

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "192 368", OffsetMax = "572 468"
                    },
                    Image =
                    {
                        Color = HexToCuiColor("#595651", 70),
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    }
                }, BotLayer, BotLayer + ".Recipes");

                var ItemSize = 85f;
                var margin = 5f;
                var xSwitch = -(_config.Receipts.Count * ItemSize + (_config.Receipts.Count - 1) * margin) / 2f;

                for (var i = 0; i < _config.Receipts.Count; i++)
                {
                    var receipt = _config.Receipts[i];

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                            OffsetMin = $"{xSwitch} {-ItemSize / 2f}",
                            OffsetMax = $"{xSwitch + ItemSize} {ItemSize / 2f}"
                        },
                        Image =
                        {
                            Color = recId == i ? HexToCuiColor("#46C1FC", 35) : HexToCuiColor("#595651", 70)
                        }
                    }, BotLayer + ".Recipes", BotLayer + $".Recipe.{i}");

                    container.Add(new CuiElement
                    {
                        Parent = BotLayer + $".Recipe.{i}",
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = !string.IsNullOrEmpty(receipt.ResultItem.Image)
                                    ? _instance.ImageLibrary.Call<string>("GetImage", receipt.ResultItem.Image)
                                    : _instance.ImageLibrary.Call<string>("GetImage", receipt.ResultItem.ShortName,
                                        receipt.ResultItem.Skin)
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1",
                                OffsetMin = "5 5", OffsetMax = "-5 -5"
                            }
                        }
                    });

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Text = { Text = "" },
                        Button =
                        {
                            Color = "0 0 0 0",
                            Command = $"UI_Drugs bot_page {i}"
                        }
                    }, BotLayer + $".Recipe.{i}");

                    xSwitch += ItemSize + margin;
                }

                #endregion

                #region Ingredients Title

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "192 343", OffsetMax = "572 363"
                    },
                    Image =
                    {
                        Color = HexToCuiColor("#595651", 85),
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    }
                }, BotLayer, BotLayer + ".Header.Ingredients");

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "5 0", OffsetMax = "-5 0"
                    },
                    Text =
                    {
                        Text = _instance.Msg(_player, IngredientsTitle),
                        Align = TextAnchor.MiddleLeft,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 14,
                        Color = "1 1 1 0.55"
                    }
                }, BotLayer + ".Header.Ingredients");

                #endregion

                #region Ingredients

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "192 218", OffsetMax = "572 338"
                    },
                    Image =
                    {
                        Color = HexToCuiColor("#595651", 70),
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    }
                }, BotLayer, BotLayer + ".Ingredients");

                var Height = 20f;
                margin = 5f;

                var ySwitch = -15f;

                if (recId >= 0 && _config.Receipts.Count > recId)
                    for (var i = 0; i < _config.Receipts[recId].Items.Count; i++)
                    {
                        var item = _config.Receipts[recId].Items[i];

                        container.Add(new CuiPanel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 1", AnchorMax = "0 1",
                                OffsetMin = $"0 {ySwitch - Height}",
                                OffsetMax = $"0 {ySwitch}"
                            },
                            Image = { Color = "0 0 0 0" }
                        }, BotLayer + ".Ingredients", BotLayer + $".Ingredient.{i}");

                        #region Amount

                        container.Add(new CuiPanel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1",
                                OffsetMin = "10 0", OffsetMax = "30 0"
                            },
                            Image =
                            {
                                Color = HexToCuiColor("#000000", 40)
                            }
                        }, BotLayer + $".Ingredient.{i}", BotLayer + $".Ingredient.{i}.Amount");

                        container.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                            Text =
                            {
                                Text = $"{item.Amount}",
                                Align = TextAnchor.MiddleCenter,
                                Font = "robotocondensed-bold.ttf",
                                FontSize = 12,
                                Color = "1 1 1 0.55"
                            }
                        }, BotLayer + $".Ingredient.{i}.Amount");

                        #endregion

                        #region Title

                        container.Add(new CuiPanel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1",
                                OffsetMin = "35 0", OffsetMax = "175 0"
                            },
                            Image =
                            {
                                Color = HexToCuiColor("#000000", 40)
                            }
                        }, BotLayer + $".Ingredient.{i}", BotLayer + $".Ingredient.{i}.Title");

                        container.Add(new CuiElement
                        {
                            Parent = BotLayer + $".Ingredient.{i}.Title",
                            Components =
                            {
                                new CuiRawImageComponent
                                {
                                    Png = !string.IsNullOrEmpty(item.Image)
                                        ? _instance.ImageLibrary.Call<string>("GetImage", item.Image)
                                        : _instance.ImageLibrary.Call<string>("GetImage", item.ShortName, item.Skin)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "0 0",
                                    OffsetMin = "2 2", OffsetMax = "18 18"
                                }
                            }
                        });

                        container.Add(new CuiLabel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1",
                                OffsetMin = "20 0", OffsetMax = "0 0"
                            },
                            Text =
                            {
                                Text = $"{item.PublicTitle}",
                                Align = TextAnchor.MiddleLeft,
                                Font = "robotocondensed-bold.ttf",
                                FontSize = 12,
                                Color = "1 1 1 0.55"
                            }
                        }, BotLayer + $".Ingredient.{i}.Title");

                        #endregion

                        #region Has Amount

                        container.Add(new CuiPanel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1",
                                OffsetMin = "180 0", OffsetMax = "250 0"
                            },
                            Image =
                            {
                                Color = HexToCuiColor("#000000", 40)
                            }
                        }, BotLayer + $".Ingredient.{i}", BotLayer + $".Ingredient.{i}.HasAmount");

                        container.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                            Text =
                            {
                                Text = _instance.Msg(_player, HasAmountTitle, item.ItemCount(_player)),
                                Align = TextAnchor.MiddleCenter,
                                Font = "robotocondensed-bold.ttf",
                                FontSize = 12,
                                Color = "1 1 1 0.55"
                            }
                        }, BotLayer + $".Ingredient.{i}.HasAmount");

                        #endregion

                        ySwitch = ySwitch - Height - margin;
                    }

                #endregion

                #region Buttons

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "1 1", AnchorMax = "1 1",
                        OffsetMin = "-125 -60", OffsetMax = "-5 -15"
                    },
                    Text =
                    {
                        Text = _instance.Msg(_player, ProcessBtn),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 18,
                        Color = "1 1 1 0.55"
                    },
                    Button =
                    {
                        Color = HexToCuiColor("#6EA642", 70),
                        Command = $"UI_Drugs bot_start {recId}"
                    }
                }, BotLayer + ".Ingredients");

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "1 1", AnchorMax = "1 1",
                        OffsetMin = "-125 -85", OffsetMax = "-5 -65"
                    },
                    Text =
                    {
                        Text = _instance.Msg(_player, InfoBtn),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 12,
                        Color = "1 1 1 0.55"
                    },
                    Button =
                    {
                        Color = HexToCuiColor("#4897D1", 70),
                        Command = $"UI_Drugs bot_info {recId}"
                    }
                }, BotLayer + ".Ingredients");

                #endregion

                CuiHelper.DestroyUi(_player, BotLayer);
                CuiHelper.AddUi(_player, container);
            }

            private void ProcessingUi()
            {
                var container = new CuiElementContainer();

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                        OffsetMin = "192 218", OffsetMax = "572 493"
                    },
                    Image =
                    {
                        Color = "0.19 0.19 0.18 0.1",
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    }
                }, "Overlay", BotLayer + ".Processing");

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                        OffsetMin = "-155 -15", OffsetMax = "155 15"
                    },
                    Image =
                    {
                        Color = HexToCuiColor("#6EA642", 30)
                    }
                }, BotLayer + ".Processing", BotLayer + ".Processing.Progress");

                var timeLeft = Time.time - _startTime;
                var progress = timeLeft / _cooldown;
                if (progress > 0)
                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = $"{progress} 1"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#6EA642", 85)
                        }
                    }, BotLayer + ".Processing.Progress");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Text =
                    {
                        Text = _instance.Msg(_player, ProcessingTitle,
                            Math.Round(_startTime + _cooldown - Time.time, 1)),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 14,
                        Color = "1 1 1 0.85"
                    }
                }, BotLayer + ".Processing.Progress");

                CuiHelper.DestroyUi(_player, BotLayer + ".Processing");
                CuiHelper.AddUi(_player, container);
            }

            #endregion

            private void OnDestroy()
            {
                CancelInvoke();

                _instance?._botComponents?.Remove(_player);

                if (_player != null)
                {
                    CuiHelper.DestroyUi(_player, BotLayer);
                    CuiHelper.DestroyUi(_player, BotLayer + ".Processing");
                }

                Destroy(this);
            }

            public void Kill()
            {
                enabled = false;

                DestroyImmediate(this);
            }
        }

        #endregion

        #region Lang

        private const string
            NoPermission = "NoPermission",
            UiTitle = "UiTitle",
            RecipeName = "RecipeName",
            IngredientsTitle = "IngredientsTitle",
            HasAmountTitle = "HasAmountTitle",
            ProcessBtn = "ProcessBtn",
            InfoBtn = "InfoBtn",
            ProcessingTitle = "ProcessingTitle",
            NeedTitle = "NeedTitle",
            ItemTitle = "ItemTitle",
            HaveTitle = "HaveTitle";

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [NoPermission] = "You don't have permission to use this command!",
                [UiTitle] = "DRUG MIXING",
                [RecipeName] = "Recipe Name",
                [IngredientsTitle] = "Ingredients",
                [HasAmountTitle] = "<color=#FAA300>{0}</color>",
                [ProcessBtn] = "PROCESS",
                [InfoBtn] = "READ ABOUT RECIPE",
                [ProcessingTitle] = "Processing in {0}s...",
                [NeedTitle] = "NEED",
                [ItemTitle] = "ITEM",
                [HaveTitle] = "HAVE"
            }, this);
        }

        private string Msg(string key, string userid = null, params object[] obj)
        {
            return string.Format(lang.GetMessage(key, this, userid), obj);
        }

        private string Msg(BasePlayer player, string key, params object[] obj)
        {
            return string.Format(lang.GetMessage(key, this, player.UserIDString), obj);
        }

        private void Reply(BasePlayer player, string key, params object[] obj)
        {
            SendReply(player, Msg(player, key, obj));
        }

        #endregion
    }
}