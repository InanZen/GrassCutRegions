using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Data;
using Terraria;
using Hooks;
using TShockAPI;
using TShockAPI.DB;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;

namespace GrassCut_Regions
{
    [APIVersion(1, 12)]
    public class GrassCutRegions : TerrariaPlugin
    {
        private static IDbConnection db;
        private static string savepath = Path.Combine(TShock.SavePath, "GrassCutRegions/");
        private static List<Region> regionList = new List<Region>();
        public override string Name
        {
            get { return "GrassCut Regions"; }
        }
        public override string Author
        {
            get { return "by InanZen"; }
        }
        public override string Description
        {
            get { return "Allows grass cutting in regions"; }
        }
        public override Version Version
        {
            get { return new Version("1.1"); }
        }
        public override void Initialize()
        {
            GameHooks.Update += OnUpdate;
            GetDataHandlers.TileEdit += TileEdit;

            try
            {
                if (!Directory.Exists(savepath))
                    Directory.CreateDirectory(savepath);
            }
            catch (Exception ex) { Log.ConsoleError(ex.ToString()); }
            Commands.ChatCommands.Add(new Command("grasscutregions", grassCutCommand, "grasscut"));
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GetDataHandlers.TileEdit -= TileEdit;
            }
            base.Dispose(disposing);
        }
        public GrassCutRegions(Main game)
            : base(game)
        {
            Order = -1;
        }
        private void SetupDb()
        {
            if (TShock.Config.StorageType.ToLower() == "sqlite")
            {
                string sql = Path.Combine(savepath, "GrassCutRegions.sqlite");
                db = new SqliteConnection(string.Format("uri=file://{0},Version=3", sql));
            }
            else if (TShock.Config.StorageType.ToLower() == "mysql")
            {
                try
                {
                    var hostport = TShock.Config.MySqlHost.Split(':');
                    db = new MySqlConnection();
                    db.ConnectionString =
                        String.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
                                      hostport[0],
                                      hostport.Length > 1 ? hostport[1] : "3306",
                                      TShock.Config.MySqlDbName,
                                      TShock.Config.MySqlUsername,
                                      TShock.Config.MySqlPassword
                            );
                }
                catch (MySqlException ex)
                {
                    Log.Error(ex.ToString());
                    throw new Exception("MySql not setup correctly");
                }
            }
            else
            {
                throw new Exception("Invalid storage type");
            }
            SqlTableCreator SQLcreator = new SqlTableCreator(db, new SqliteQueryCreator());
            var table = new SqlTable("Regions",
                 new SqlColumn("ID", MySqlDbType.Int32) { Primary = true, AutoIncrement = true },
                 new SqlColumn("Name", MySqlDbType.VarChar, 255) { Unique = true },
                 new SqlColumn("WorldID", MySqlDbType.Int32)
             );
            SQLcreator.EnsureExists(table);
        }
        public void TileEdit(Object sender, GetDataHandlers.TileEditEventArgs args)
        {
            if ((args.EditType == 0 || args.EditType == 4) && new int[] { 3, 24, 32, 51, 52, 61, 62, 69, 71, 73, 74, 80, 82, 83, 84, 110, 113, 115, 138 }.Contains(Main.tile[args.X, args.Y].type))
            {
                try
                {
                    foreach (Region reg in regionList)
                    {
                        if (reg != null && reg.InArea(args.X, args.Y))
                        {
                            WorldGen.KillTile(args.X, args.Y);
                            args.Player.SendTileSquare(args.X, args.Y, 3);
                            args.Handled = true;
                            break;
                        }
                    }
                }
                catch (Exception ex) { Log.ConsoleError(ex.ToString()); }
            }
        }
        public static void grassCutCommand(CommandArgs args)
        {
            try
            {
                string cmd = "";
                if (args.Parameters.Count > 0)
                {
                    cmd = args.Parameters[0].ToLower();
                }
                switch (cmd)
                {
                    case "add":
                        {
                            if (args.Parameters.Count > 1)
                            {
                                string regionName = String.Join(" ", args.Parameters.GetRange(1, args.Parameters.Count - 1));
                                var region = TShock.Regions.GetRegionByName(regionName);
                                if (region != null && region.Name != "")
                                {
                                    if (regionList.Contains(region))
                                    {
                                        args.Player.SendMessage("Region already added", Color.Red);
                                        break;
                                    }
                                    if (AddRegion(region))
                                    {
                                        args.Player.SendMessage(String.Format("Region '{0}' added to GrassCut list", region.Name), Color.Green);
                                        break;
                                    }

                                }
                                else
                                    args.Player.SendMessage("Region \"" + regionName + "\" does not exist", Color.Red);
                            }
                            else
                                args.Player.SendMessage("Invalid Syntax. Use: /grasscut add RegionName", Color.Red);
                            break;
                        }
                    case "delete":
                    case "remove":
                        {
                            if (args.Parameters.Count > 1)
                            {
                                string regionName = String.Join(" ", args.Parameters.GetRange(1, args.Parameters.Count - 1));
                                foreach (Region reg in regionList)
                                {
                                    if (reg != null && reg.Name == regionName)
                                    {
                                        db.Query("DELETE FROM Regions WHERE Name = @0 AND WorldID = @0", regionName, Main.worldID);
                                        regionList.Remove(reg);
                                        args.Player.SendMessage(String.Format("Region '{0}' removed", regionName), Color.Yellow);
                                        return;
                                    }
                                }
                                args.Player.SendMessage("Region not on the GrassCut list", Color.Red);
                            }
                            else
                                args.Player.SendMessage("Invalid Syntax. Use: /grasscut remove|delete RegionName", Color.Red);
                            break;
                        }
                    default:
                        {
                            args.Player.SendMessage("Available commands:", Color.Yellow);
                            args.Player.SendMessage("/grasscut add RegionName", Color.Yellow);
                            args.Player.SendMessage("/grasscut remove|delete RegionName", Color.Yellow);
                            break;
                        }
                }
            }
            catch (Exception ex) { Log.ConsoleError(ex.ToString()); }
        }
        public static bool AddRegion(Region region)
        {
            try
            {
                db.Query("INSERT INTO Regions (Name, WorldID) VALUES (@0, @1)", region.Name, Main.worldID);
                regionList.Add(region);
                return true;
            }
            catch (Exception ex) { Log.ConsoleError(ex.ToString()); }
            return false;
        }
        void OnUpdate()
        {
            if (Main.worldID != 0)
            {
                using (QueryResult reader = db.QueryReader("SELECT Name FROM Regions WHERE WorldID = @0", Main.worldID))
                {
                    try
                    {
                        while (reader.Read())
                        {
                            var region = TShock.Regions.GetRegionByName(reader.Get<string>("Name"));
                            if (region != null && region.Name != "")
                                regionList.Add(region);
                        }
                    }
                    catch (Exception ex) { Log.ConsoleError(ex.ToString()); }
                }
                GameHooks.Update -= OnUpdate;
            }
        }
    }
}
