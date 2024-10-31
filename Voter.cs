using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Voter", "sami37", "1.0.0", ResourceId = 0)]
    [Description("Vote and rewards")]
    class Voter : RustPlugin
    {
        [PluginReference("Economics")]
        private Plugin Economics;

        private Voter instance;
        private bool newConfig, ChatPlayerIcon;
        private string ChatFormat, ChatName;
        Dictionary<string, string> ITEMS = new Dictionary<string, string>();
        private List<object> help;
        private DynamicConfigFile DataFile;
        private Dictionary<ulong, int> pointData = new Dictionary<ulong, int>();
        private List<object> Reward = new List<object>();
        private Dictionary<string, object> TrackerSetting = new Dictionary<string, object>();

        private static Dictionary<string, Trackers> TRACKER_API = new Dictionary<string, Trackers>
        {
            { "[TopRustServers]", new Trackers { API = "http://api.toprustservers.com/api/put?plugin=voter&key={0}&uid={1}", VoteUrl = "toprustservers.com/server/{0}" }},
            { "[Rust-Servers]", new Trackers { API = "http://rust-servers.net/api/?action=post&object=votes&element=claim&key={0}&steamid={1}", VoteUrl = "rust-servers.net/server/{0}" }},
            { "[Rust-ServerList]", new Trackers { API = "http://rust-serverlist.net/api.php?apikey={0}&mode=vote&uid={1}", VoteUrl = "rust-serverlist.net/server.php?id={0}" }}
        };

        void SendMessage(BasePlayer player, string msg, string chatname = null)
        {
            string icon = ChatPlayerIcon && rust.UserIDFromPlayer(player) != null ? rust.UserIDFromPlayer(player) : "0";
            player.SendConsoleCommand("chat.add", icon, ChatFormat.Replace("{NAME}", chatname ?? ChatName).Replace("{MESSAGE}", msg));
        }

        void OnServerInitialized()
        {
            var messages = new Dictionary<string, string>
            {
                {"RewardGived", "You got your reward!"},
                {"RewardNotFound", "Reward with this ID cann't be found!"},
                {"RewardNotPoints", "You do not have enough points for this reward!"},
                {"RewardsBalance", "Your points: {0}"},
                {"RewardsBegin", "-------- /rewards [id] (Get Reward) --------"},
                {"RewardsEnd", "-------------------------------------------------"},
                {"RewardsList", "<color=red>ID:{2}</color> -Points: <color=cyan>{0}</color>, Reward: <color=cyan>{1}</color>"},
                {"StatusBadApiKey", "Invalid API key."},
                {"StatusCanVote", "You can vote at '{0}' (Points for voting: {1})"},
                {"StatusGetPoint", "Thanks for vote! (Points received: {0})"},
                {"StatusNotAvailable", "The tracker is not available now. Please try again later."}
            };
            lang.RegisterMessages(messages, this);
            LoadConfig();
            LoadData();
            var it = ItemManager.GetItemDefinitions().GetEnumerator();
	        while (it.MoveNext())
	                ITEMS[it.Current?.shortname] = it.Current?.displayName.translated;
            SavePoints();
        }

        void LoadData()
        {
            DataFile = Interface.Oxide.DataFileSystem.GetFile("VoterData");
            try
            {
                pointData = DataFile.ReadObject<Dictionary<ulong, int>>();
            }
            catch (Exception e)
            {
                pointData = new Dictionary<ulong, int>();
                PrintWarning("Failed to load DataFile, creating new file.");
            }
        }

        void Unload()
        {
            SavePoints();
        }

        void SavePoints()
        {
            if (pointData == null) return;
            DataFile.WriteObject(pointData);
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadConfig();
            LoadData();
        }

        void LoadConfig()
        {
            SetConfig("Settings", "ChatFormat", "<color=#af5>{NAME}:</color> {MESSAGE}");
            SetConfig("Settings", "ChatName", "[Voter]");
            SetConfig("Settings", "ChatPlayerIcon", true);
            SetConfig("Settings", "ChatFormat", "<color=#af5>{NAME}:</color> {MESSAGE}");
            SetConfig("Settings", "Help", Help());
            SetConfig("Settings", "Rewards", DefaultItemList());
            SetConfig("Settings", "Trackers", "[Rust-ServerList]", new Tracker());
            SetConfig("Settings", "Trackers", "[Rust-Servers]", new Tracker());
            SetConfig("Settings", "Trackers", "[TopRustServers]", new Tracker());

            ChatFormat = GetConfig<string>( "<color=#af5>{NAME}:</color> {MESSAGE}", "Settings", "ChatFormat");
            ChatName = GetConfig<string>("[Voter]", "Settings", "ChatName");
            ChatPlayerIcon = GetConfig<bool>(true, "Settings", "ChatPlayerIcon");
            help = GetConfig( Help(), "Settings", "Help");
            Reward = GetConfig(DefaultItemList(), "Settings", "Rewards");
            TrackerSetting = GetConfig(new Dictionary<string, object>(), "Settings", "Trackers");
            if (!newConfig) return;
            SaveConfig();
            newConfig = false;
        }

		static List<object> DefaultItemList()
		{
            
		    var dp00 = new Dictionary<string, object> {{"wall.external.high.stone", 100}};
		    var dp0 = new Dictionary<string, object> {{"reward", dp00}, {"price", 1}};

		    var dp10 = new Dictionary<string, object> {{"weapon.mod.small.scope", 4}};
		    var dp1 = new Dictionary<string, object> {{"reward", dp10}, {"price", 2}};

		    var dp20 = new Dictionary<string, object> {{"weapon.mod.silencer", 4}};
		    var dp2 = new Dictionary<string, object> {{"reward", dp20}, {"price", 2}};

		    var dp30 = new Dictionary<string, object> {{"ammo.rifle.hv", 300}, {"metal.refined", 10000}};
		    var dp3 = new Dictionary<string, object> {{"reward", dp30}, {"price", 1}};

		    var dp40 = new Dictionary<string, object> {{"explosive.timed", 75}, {"autoturret", 20}};
		    var dp4 = new Dictionary<string, object> {{"reward", dp40}, {"price", 14}};

		    var dp = new List<object>();
		    dp.Add(dp0);
		    dp.Add(dp1);
		    dp.Add(dp2);
		    dp.Add(dp3);
		    dp.Add(dp4);

		    return dp;
		}

        class Rewards
        {
            public int price;
            public Dictionary<string, object> reward;
        }

        class Tracker
        {
            public Dictionary<string, string> ID;
            public Dictionary<string, string> Key;
            public Dictionary<string, string> PoIntsForVote;
        }

        class Trackers
        {
            public string API;
            public string VoteUrl;

            public string GetUrl(string key, string steamID)
            {
                return string.Format(API, key, steamID);
            }

            public string GetVoteUrl(string identity)
            {
                return string.Format(VoteUrl, identity);
            }
        }

        private List<object> Help()
        {
            List<object> d = new List<object>
            {
                "/vote -- show a list of available urls for voting and get points for voting",
                "/rewards -- show the rewards information and your points",
                "/rewards [ID] -- get the reward"
            };
            return d;
        }

        void SendHelpText(BasePlayer player)
        {
            foreach (var text in help)
            {
                SendMessage(player, text.ToString());
            }
        }


        [ChatCommand("vote")]
        void cmdChatVote(BasePlayer player, string commands, string[] args)
        {
            foreach (var key in TrackerSetting.Keys)
            {
                Trackers trackAPI = TRACKER_API[key];
                if (trackAPI == null) continue;

                Dictionary<string, object> currentTrack = (Dictionary<string, object>) TrackerSetting[key];
                if (currentTrack != null)
                {
                    string url = trackAPI.GetUrl((string)currentTrack["Key"], player.UserIDString);
                    if((string)currentTrack["Key"] != "")
                    webrequest.EnqueueGet(url, (code, result) =>
                    {
                        if (code == 200)
                        {
                            if (result == "1")
                            {
                                if(pointData == null)
                                    pointData = new Dictionary<ulong, int>();
                                if (!pointData.ContainsKey(player.userID))
                                {
                                    object point = 0;
                                    currentTrack.TryGetValue("PointsForVote", out point);
                                    pointData.Add(player.userID, (int)point);
                                    SavePoints();
                                }
                                else
                                {
                                    int point = pointData[player.userID];
                                    point += Convert.ToInt32(Convert.ToString(currentTrack["PointsForVote"]));
                                    pointData.Remove(player.userID);
                                    pointData.Add(player.userID, point);
                                    SavePoints();
                                }
                                if (key == "[Rust-ServerList]")
                                    webrequest.EnqueueGet(url.Replace("vote", "claimed"), (codes, results) => { }, this);
                            }
                            else if (result == "API NOT SET UP" || result == "Error: incorrect server key" ||
                                     result == "Bad APIKEY")
                            {
                                SendMessage(player, lang.GetMessage("StatusBadApiKey", this, player.UserIDString));
                            }
                            else
                            {
                                SendMessage(player, string.Format(lang.GetMessage("StatusCanVote", this, player.UserIDString), trackAPI.GetVoteUrl((string)currentTrack["ID"]), currentTrack["PointsForVote"]));
                            }
                        }
                        else
                        {
                            SendMessage(player, lang.GetMessage("StatusNotAvailable", this, player.UserIDString));
                        }
                    }, this);
                }
            }
        }

        [ChatCommand("reward")]
        void cmdChatReward(BasePlayer player, string commands, string[] args)
        {
            if (args != null && args.Length > 0)
            {
                if (pointData != null && pointData.ContainsKey(player.userID))
                {
                    var points = pointData[player.userID];
                    var index = Convert.ToInt32(args[0]) - 1;

                    if (Reward.ElementAtOrDefault(index) != null)
                    {
                        var price = 0;
                        Dictionary<string, object> it = (Dictionary<string, object>)Reward[index];
                        foreach (var item in it)
                        {
                            if (item.Key == "price")
                            {
                                price =  Convert.ToInt32(item.Value);
                            }
                            if (item.Key == "reward")
                            {
								if (price > points)
								{
									SendMessage(player, lang.GetMessage("RewardNotPoints", this, player.UserIDString));
									return;
								}
                                foreach (var items in (Dictionary<string, object>)item.Value)
                                {
                                    Item giveItem = ItemManager.CreateByName(items.Key, Convert.ToInt32(items.Value));
                                    if (giveItem != null)
                                    {
                                        if (!giveItem.MoveToContainer(player.inventory.containerMain) &&
                                            !giveItem.MoveToContainer(player.inventory.containerBelt))
                                        {
                                            giveItem.Drop(player.eyes.position, player.eyes.BodyForward()*2f);
                                        }
                                    }
                                }
                            }
                        }
                        points -= price;
                        pointData.Remove(player.userID);
                        pointData.Add(player.userID, points);
                        SendMessage(player, lang.GetMessage("RewardGived", this, player.UserIDString));
                        SavePoints();
                    }
                    else
                    {
                        SendMessage(player, lang.GetMessage("RewardNotFound", this, player.UserIDString));
                    }
                }
                else
                {
                    SendMessage(player, lang.GetMessage("RewardNotPoints", this, player.UserIDString));
                }
            }
            else
            {
                var point = 0;
				if(pointData != null && pointData.ContainsKey(player.userID))
					point = pointData[player.userID];
                SendMessage(player, lang.GetMessage("RewardsBegin", this, player.UserIDString));
                SendMessage(player, string.Format(lang.GetMessage("RewardsBalance", this, player.UserIDString), point));
                
				if(Reward != null)
                for(var i = 0; i < Reward.Count; i++)
                {
                    var price = 0;
                    List<string> stringReward = new List<string>();
                    Dictionary<string, object> it = (Dictionary<string, object>)Reward[i];
                    foreach (var item in it)
                    {
                        if (item.Key == "price")
                        {
                            price =  Convert.ToInt32(item.Value);
                        }
                        if (item.Key == "reward")
                        {
                            foreach (var items in (Dictionary<string, object>)item.Value)
                            {
                                Item giveItem = ItemManager.CreateByName(items.Key, Convert.ToInt32(items.Value));
                                if(giveItem != null)
                                    stringReward.Add(giveItem.info.displayName.english + " x" + items.Value);
                            }
                        }
                    }
                    string[] newList = {};
                    newList = stringReward.ToArray();
                    SendMessage(player, string.Format(lang.GetMessage("RewardsList", this, player.UserIDString), price, string.Join(", ", newList), (i+1).ToString()));
                }
                SendMessage(player, lang.GetMessage("RewardsEnd", this, player.UserIDString));
            }
        }

        T GetConfig<T>(T defaultVal, params object[] args) { List<string> stringArgs = (from arg in args select arg.ToString()).ToList(); if (Config.Get(stringArgs.ToArray()) == null) { PrintError($"The plugin failed to read something from the config: {ListToString(stringArgs, 0, "/")}{Environment.NewLine}Please reload the plugin and see if this message is still showing. If so, please post this into the support thread of this plugin."); return defaultVal; } return (T)System.Convert.ChangeType(Config.Get(stringArgs.ToArray()), typeof(T)); }
        
        void SetConfig(params object[] args) { List<string> stringArgs = (from arg in args select arg.ToString()).ToList(); stringArgs.RemoveAt(args.Length - 1); if (Config.Get(stringArgs.ToArray()) == null) Config.Set(args); }
        
        string ListToString<T>(List<T> list, int first = 0, string seperator = ", ") => string.Join(seperator, (from val in list select val.ToString()).Skip(first).ToArray());
    }
}