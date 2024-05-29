using System;
using MySql.Data.MySqlClient;
using System.Data;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DevelopersHub.RealtimeNetworking.Server
{
    class Database
    {

        #region MySQL
       
        private static MySqlConnection _mysqlConnection;
        private const string _mysqlServer = "127.0.0.1";
        private const string _mysqlUsername = "root";
        private const string _mysqlPassword = "";
        private const string _mysqlDatabase = "shigen_senso";
        

        public static MySqlConnection mysqlConnection
        {
            get
            {
                if (_mysqlConnection == null || _mysqlConnection.State == ConnectionState.Closed)
                {
                    try
                    {
                        _mysqlConnection = new MySqlConnection("SERVER=" + _mysqlServer + "; DATABASE=" + _mysqlDatabase + "; UID=" + _mysqlUsername + "; PASSWORD=" + _mysqlPassword + ";");
                        _mysqlConnection.Open();
                        Console.WriteLine("Connection established with MySQL database.");
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Failed to connect the MySQL database.");
                    }
                }
                else if (_mysqlConnection.State == ConnectionState.Broken)
                {
                    try
                    {
                        _mysqlConnection.Close();
                        _mysqlConnection = new MySqlConnection("SERVER=" + _mysqlServer + "; DATABASE=" + _mysqlDatabase + "; UID=" + _mysqlUsername + "; PASSWORD=" + _mysqlPassword + ";");
                        _mysqlConnection.Open();
                        Console.WriteLine("Connection re-established with MySQL database.");
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Failed to connect the MySQL database.");
                    }
                }
                return _mysqlConnection;
            }
        }

        public static MySqlConnection GetMySqlConnection(){

            MySqlConnection connection = new MySqlConnection("SERVER=" + _mysqlServer + "; DATABASE=" + _mysqlDatabase + "; UID=" + _mysqlUsername + "; PASSWORD=" + _mysqlPassword + "; POOLING=TRUE");
            connection.Open();
            return connection;
        }

        private static DateTime collectTime = DateTime.Now;        

        public static void Update()
        {
            double deltaTime = (DateTime.Now - collectTime).TotalSeconds;

            if((DateTime.Now - collectTime).TotalSeconds >= 1)
            {
                collectTime = DateTime.Now;
                CollectResources();
                UpdateUnitTraining(deltaTime);
            }
        }

        public async static void AuthenticatePlayer(int id, string device)
        {
            Data.InitializationData auth = await AuthenticatePlayerAsync(device);          
            Server.clients[id].device = device;
            Server.clients[id].account = auth.accountID;

            string authData = await Data.Serialize<Data.InitializationData>(auth);

            await UpdateHasCastleAsync(auth.accountID, 0);
            await UpdatePlayerResourcesAsync(auth.accountID, 10, 100, 3000, 3000, 3000, 0, 0, 0);

            Packet packet = new Packet();

            packet.Write((int)Terminal.RequestsID.AUTH);
            packet.Write(authData);

            Sender.TCP_Send(id, packet);
        }

        private async static Task<Data.InitializationData> AuthenticatePlayerAsync(string device)
        {
            Task<Data.InitializationData> task = Task.Run(() =>
            {
                Data.InitializationData initializationData = new Data.InitializationData();
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string select_query = String.Format("SELECT id FROM accounts WHERE device_id = '{0}';", device);
                    bool userFound = false;
                    using (MySqlCommand select_command = new MySqlCommand(select_query, connection))
                    {
                        using (MySqlDataReader reader = select_command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    initializationData.accountID = long.Parse(reader["id"].ToString());
                                    userFound = true;
                                }
                            }
                        }
                    }
                    if (!userFound)
                    {
                        string insert_query = String.Format("INSERT INTO accounts (device_id) VALUES ('{0}');", device);
                        using (MySqlCommand insert_command = new MySqlCommand(insert_query, connection))
                        {
                            insert_command.ExecuteNonQuery();
                            initializationData.accountID = insert_command.LastInsertedId;
                        }
                    }
                    initializationData.serverUnits = GetServerUnits(connection);
                }              

                return initializationData;
            });
            return await task;
        }

          
        public async static void GetPlayerData(int id)
        {
            long accountID = Server.clients[id].account;
            Data.Player data_player = await GetPlayerDataAsync(accountID);
            List<Data.Unit> units = await GetUnitsAsync(accountID);

            data_player.units = units;

            Packet packet = new Packet();

            packet.Write((int)Terminal.RequestsID.SYNC);
            string player = await Data.Serialize<Data.Player>(data_player);
            packet.Write(player);
     
            Sender.TCP_Send(id, packet);
        }

        private async static Task<Data.Player> GetPlayerDataAsync(long accountID)
        {
            Task<Data.Player> task = Task.Run(() =>
            {
                Data.Player data = new Data.Player();

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string select_query = String.Format("SELECT id, gold, gems, stone, wood, food, stone_production, wood_production, food_production, has_castle FROM accounts WHERE id = '{0}';", accountID);
                    using (MySqlCommand select_command = new MySqlCommand(select_query, connection))
                    {
                        using (MySqlDataReader reader = select_command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    // data.id = long.Parse(reader["id"].ToString());
                                    data.gold = int.Parse(reader["gold"].ToString());
                                    data.gems = int.Parse(reader["gems"].ToString());
                                    data.stone = int.Parse(reader["stone"].ToString());
                                    data.wood = int.Parse(reader["wood"].ToString());
                                    data.food = int.Parse(reader["food"].ToString());
                                    data.stoneProduction = int.Parse(reader["stone_production"].ToString());
                                    data.woodProduction = int.Parse(reader["wood_production"].ToString());
                                    data.foodProduction = int.Parse(reader["food_production"].ToString());
                                    data.hasCastle = bool.Parse(reader["has_castle"].ToString());

                                }
                            }
                        }
                    }
                }                  
                return data;
            });
            return await task;
        }



        public async static void BuildCastle(int id, int x_pos, int y_pos)
        {
            int result = 0;
            long accountID = Server.clients[id].account;

            Data.Player player = await GetPlayerDataAsync(accountID);
            if(player.hasCastle == false)
            {
                int selectedTileTyle = await GetHexTileTypeAsync(x_pos, y_pos);

                Data.HexTile castleTile = new Data.HexTile();
                castleTile.x = x_pos;
                castleTile.y = y_pos;
                castleTile.hexType = selectedTileTyle;

                if ((Terminal.HexType)selectedTileTyle == Terminal.HexType.FREE_LAND)
                {
                    await UpdateHasCastleAsync(accountID, 1);                                       
                    await UpdateHexTileTypeAsync(x_pos, y_pos, Terminal.HexType.PLAYER_CASTLE);
                    await UpdateCastleNeighboursAsync(castleTile);
                    player.hasCastle = true;
                    result = 1;
                }
            }

            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.BUILD_CASTLE);
            packet.Write(result);
            Sender.TCP_Send(id, packet);
        }

        public async static void BuildStoneMine(int id, int x_pos, int y_pos)
        {
            long accountID = Server.clients[id].account;

            int result = 0;
                                   
            int selectedTileTyle = await GetHexTileTypeAsync(x_pos, y_pos);

            Data.HexTile stoneMineTile = new Data.HexTile();
            stoneMineTile.x = x_pos;
            stoneMineTile.y = y_pos;
            stoneMineTile.hexType = selectedTileTyle;

            if ((Terminal.HexType)selectedTileTyle == Terminal.HexType.PLAYER_LAND)
            {
                Data.Player player = await GetPlayerDataAsync(accountID);
                Data.ServerBuilding serverBuilding = await GetServerBuildingAsync("stone_mine", 1);             

                if (player.gold >= serverBuilding.requiredGold && player.stone >= serverBuilding.requiredStone && player.wood >= serverBuilding.requiredWood)
                {
                    await UpdateHexTileTypeAsync(x_pos, y_pos, Terminal.HexType.PLAYER_STONE_MINE);

                    List<Data.HexTile> neighbours = await GetNeighboursAsync(stoneMineTile);

                    int stonePerSecond = 0;
                    foreach (Data.HexTile neighbour in neighbours)
                    {
                        if (neighbour.hexType == (int)Terminal.HexType.PLAYER_MOUNTAIN)
                        {
                            stonePerSecond += serverBuilding.stonePerSecond;
                        }
                    }
                    await UpdatePlayerResourcesAsync(accountID, player.gems, player.gold - serverBuilding.requiredGold, player.stone - serverBuilding.requiredStone, player.wood - serverBuilding.requiredWood, player.food, player.stoneProduction + stonePerSecond, player.woodProduction, player.foodProduction);
                    await UpdateBuildingProductionAsync(stonePerSecond, 0, 0, x_pos, y_pos);

                    result = 1;
                }
                else
                {
                    result = 2;
                }
                                                                         
            }

            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.BUILD_STONE_MINE);
            packet.Write(result);
            Sender.TCP_Send(id, packet);
        }

        public async static void BuildSawmill(int id, int x_pos, int y_pos)
        {
            long accountID = Server.clients[id].account;

            int result = 0;

            int selectedTileTyle = await GetHexTileTypeAsync(x_pos, y_pos);

            Data.HexTile sawmillTile = new Data.HexTile();
            sawmillTile.x = x_pos;
            sawmillTile.y = y_pos;
            sawmillTile.hexType = selectedTileTyle;

            if ((Terminal.HexType)selectedTileTyle == Terminal.HexType.PLAYER_LAND)
            {
                Data.Player player = await GetPlayerDataAsync(accountID);
                Data.ServerBuilding serverBuilding = await GetServerBuildingAsync("sawmill", 1);
              

                if (player.gold >= serverBuilding.requiredGold && player.stone >= serverBuilding.requiredStone && player.wood >= serverBuilding.requiredWood)
                {
                    await UpdateHexTileTypeAsync(x_pos, y_pos, Terminal.HexType.PLAYER_SAWMILL);

                    List<Data.HexTile> neighbours = await GetNeighboursAsync(sawmillTile);

                    int woodPerSecond = 0;
                    foreach (Data.HexTile neighbour in neighbours)
                    {
                        if (neighbour.hexType == (int)Terminal.HexType.PLAYER_FOREST)
                        {
                            woodPerSecond += serverBuilding.woodPerSecond;
                        }
                    }
                    await UpdatePlayerResourcesAsync(accountID, player.gems, player.gold - serverBuilding.requiredGold, player.stone - serverBuilding.requiredStone, player.wood - serverBuilding.requiredWood, player.food, player.stoneProduction, player.woodProduction + woodPerSecond, player.foodProduction);
                    await UpdateBuildingProductionAsync(0, woodPerSecond, 0, x_pos, y_pos);

                    result = 1;
                }
                else
                {
                    result = 2;
                }

            }

            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.BUILD_SAWMILL);
            packet.Write(result);
            Sender.TCP_Send(id, packet);
        }

        public async static void BuildFarm(int id, int x_pos, int y_pos)
        {
            long accountID = Server.clients[id].account;

            int result = 0;

            int selectedTileTyle = await GetHexTileTypeAsync(x_pos, y_pos);

            Data.HexTile farmTile = new Data.HexTile();
            farmTile.x = x_pos;
            farmTile.y = y_pos;
            farmTile.hexType = selectedTileTyle;

            if ((Terminal.HexType)selectedTileTyle == Terminal.HexType.PLAYER_LAND)
            {
                Data.Player player = await GetPlayerDataAsync(accountID);
                Data.ServerBuilding serverBuilding = await GetServerBuildingAsync("farm", 1);
                 

                if (player.gold >= serverBuilding.requiredGold && player.stone >= serverBuilding.requiredStone && player.wood >= serverBuilding.requiredWood)
                {
                    await UpdateHexTileTypeAsync(x_pos, y_pos, Terminal.HexType.PLAYER_FARM);

                    List<Data.HexTile> neighbours = await GetNeighboursAsync(farmTile);

                    int foodPerSecond = 0;
                    foreach (Data.HexTile neighbour in neighbours)
                    {
                        if (neighbour.hexType == (int)Terminal.HexType.PLAYER_CROPS)
                        {
                            foodPerSecond += serverBuilding.foodPerSecond;
                        }
                    }
                    await UpdatePlayerResourcesAsync(accountID, player.gems, player.gold - serverBuilding.requiredGold, player.stone - serverBuilding.requiredStone, player.wood - serverBuilding.requiredWood, player.food, player.stoneProduction, player.woodProduction, player.foodProduction + foodPerSecond);
                    await UpdateBuildingProductionAsync(0, 0, foodPerSecond, x_pos, y_pos);

                    result = 1;
                }
                else
                {
                    result = 2;
                }

            }

            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.BUILD_FARM);
            packet.Write(result);
            Sender.TCP_Send(id, packet);
        }

        public async static void BuildArmyCamp(int id, int x_pos, int y_pos)
        {
            long accountID = Server.clients[id].account;

            int result = 0;

            int selectedTileTyle = await GetHexTileTypeAsync(x_pos, y_pos);

            Data.HexTile armyCampTile = new Data.HexTile();
            armyCampTile.x = x_pos;
            armyCampTile.y = y_pos;
            armyCampTile.hexType = selectedTileTyle;

            if ((Terminal.HexType)selectedTileTyle == Terminal.HexType.FREE_LAND)
            {
                Data.Player player = await GetPlayerDataAsync(accountID);
                Data.ServerBuilding serverBuilding = await GetServerBuildingAsync("army_camp", 1);

                if(player.hasCastle == true)
                {
                    if (player.gold >= serverBuilding.requiredGold && player.stone >= serverBuilding.requiredStone && player.wood >= serverBuilding.requiredWood)
                    {             
                        List<Data.HexTile> neighbours = await Get2RingsOfNeighboursAsync(armyCampTile);
                        bool canBuild = false;
                        
                        foreach (Data.HexTile neighbour in neighbours)
                        {
                            if (neighbour.hexType == (int)Terminal.HexType.PLAYER_LAND || neighbour.hexType == (int)Terminal.HexType.PLAYER_MOUNTAIN || neighbour.hexType == (int)Terminal.HexType.PLAYER_FOREST || neighbour.hexType == (int)Terminal.HexType.PLAYER_CROPS || neighbour.hexType == (int)Terminal.HexType.PLAYER_STONE_MINE || neighbour.hexType == (int)Terminal.HexType.PLAYER_SAWMILL || neighbour.hexType == (int)Terminal.HexType.PLAYER_FARM || neighbour.hexType == (int)Terminal.HexType.PLAYER_ARMY_CAMP)
                            {
                                canBuild = true;
                                break;
                            }
                        }

                        if (canBuild)
                        {
                            await UpdateHexTileTypeAsync(x_pos, y_pos, Terminal.HexType.PLAYER_ARMY_CAMP);
                            await UpdateArmyCampNeighboursAsync(armyCampTile);
                            await UpdatePlayerResourcesAsync(accountID, player.gems, player.gold - serverBuilding.requiredGold, player.stone - serverBuilding.requiredStone, player.wood - serverBuilding.requiredWood, player.food, player.stoneProduction, player.woodProduction, player.foodProduction);
                            result = 3;
                        }
                        else
                        {
                            result = 2;
                        }

                        
                    }
                    else
                    {
                        result = 1;
                    }
                }
             
            }

            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.BUILD_ARMY_CAMP);
            packet.Write(result);
            Sender.TCP_Send(id, packet);
        }
       

        
        private async static Task<Data.ServerBuilding> GetServerBuildingAsync(string buildingID, int level)
        {
            Task<Data.ServerBuilding> task = Task.Run(() =>
            {
                Data.ServerBuilding data = new Data.ServerBuilding();

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string select_query = String.Format("SELECT id, required_gold, required_wood, required_stone, stone_per_second, wood_per_second, food_per_second, health, max_capacity FROM server_buildings WHERE global_id = '{0}' AND level = {1};", buildingID, level);
                    using (MySqlCommand select_command = new MySqlCommand(select_query, connection))
                    {
                        using (MySqlDataReader reader = select_command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    data.id = buildingID;
                                    data.databaseID = long.Parse(reader["id"].ToString());
                                    data.level = level;
                                    data.requiredGold = int.Parse(reader["required_gold"].ToString());
                                    data.requiredWood = int.Parse(reader["required_wood"].ToString());
                                    data.requiredStone = int.Parse(reader["required_stone"].ToString());
                                    data.stonePerSecond = int.Parse(reader["stone_per_second"].ToString());
                                    data.woodPerSecond = int.Parse(reader["wood_per_second"].ToString());
                                    data.foodPerSecond = int.Parse(reader["food_per_second"].ToString());
                                    data.health = int.Parse(reader["health"].ToString());
                                    data.max_capacity = int.Parse(reader["max_capacity"].ToString());
                                }
                            }
                        }
                    }
                }                
                return data;
            });
            return await task;
        }

        private async static Task<Data.HexTile> GetArmyCampDataAsync(int x, int y)
        {
            Task<Data.HexTile> task = Task.Run(() =>
            {
                Data.HexTile data = new Data.HexTile();

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string select_query = String.Format("SELECT x, y, health, capacity FROM hex_grid WHERE x = {0} AND y = {1};", x, y);
                    using (MySqlCommand select_command = new MySqlCommand(select_query, connection))
                    {
                        using (MySqlDataReader reader = select_command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {                                    
                                    data.x = int.Parse(reader["x"].ToString());
                                    data.y = int.Parse(reader["y"].ToString());
                                    data.health = int.Parse(reader["health"].ToString());
                                    data.capacity = int.Parse(reader["capacity"].ToString());
                                }
                            }
                        }
                    }
                }
                return data;
            });
            return await task;
        }


        private static List<Data.ServerUnit> GetServerUnits(MySqlConnection connection)
        {
            List<Data.ServerUnit> units = new List<Data.ServerUnit>();
            string query = String.Format("SELECT global_id, level, required_food, train_time, health, housing FROM server_units;");
            using (MySqlCommand command = new MySqlCommand(query, connection))
            {
                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            Data.ServerUnit unit = new Data.ServerUnit();
                            unit.id = (Data.UnitID)Enum.Parse(typeof(Data.UnitID), reader["global_id"].ToString());
                            int.TryParse(reader["level"].ToString(), out unit.level);
                            int.TryParse(reader["required_food"].ToString(), out unit.requiredFood);
                            int.TryParse(reader["train_time"].ToString(), out unit.trainTime);
                            int.TryParse(reader["health"].ToString(), out unit.health);
                            int.TryParse(reader["housing"].ToString(), out unit.housing);
                            units.Add(unit);
                        }
                    }
                }
            }
            return units;
        }

        private static Data.ServerUnit GetServerUnit(MySqlConnection connection, string unitID, int level)
        {
            Data.ServerUnit unit = new Data.ServerUnit();
            string query = String.Format("SELECT global_id, level, required_food, train_time, health, housing FROM server_units WHERE global_id = '{0}' AND level = {1};", unitID, level);
            using (MySqlCommand command = new MySqlCommand(query, connection))
            {
                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {                                                
                            unit.id = (Data.UnitID)Enum.Parse(typeof(Data.UnitID), reader["global_id"].ToString());
                            int.TryParse(reader["level"].ToString(), out unit.level);
                            int.TryParse(reader["required_food"].ToString(), out unit.requiredFood);
                            int.TryParse(reader["train_time"].ToString(), out unit.trainTime);
                            int.TryParse(reader["health"].ToString(), out unit.health);
                            int.TryParse(reader["housing"].ToString(), out unit.housing);                           
                        }
                    }
                }
            }
            return unit;
        }


        public async static void GenerateNewGrid(int id)
        {
            //TODO: Game id
            Data.HexGrid hexGrid = await GenerateGridAsync();       

            Packet packet = new Packet();
            packet.Write(3);
            string grid = await Data.Serialize<Data.HexGrid>(hexGrid);
            packet.Write(grid);
            Sender.TCP_Send(id, packet);
           
        }

        private async static Task<Data.HexGrid> GenerateGridAsync()
        {
            Task<Data.HexGrid> task = Task.Run(() =>
            {
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string delete_query = String.Format("DELETE FROM hex_grid;");
                    using (MySqlCommand delete_command = new MySqlCommand(delete_query, connection))
                    {
                        delete_command.ExecuteNonQuery();
                    }

                    Data.HexGrid hexGrid = new Data.HexGrid();

                    for (int x = 0; x < hexGrid.rows; x++)
                    {
                        for (int y = 0; y < hexGrid.columns; y++)
                        {
                            int hexTileType = GetRandomHexTile();

                            Data.HexTile tile = new Data.HexTile();
                            tile.x = x;
                            tile.y = y;
                            tile.hexType = hexTileType;

                            hexGrid.hexTiles.Add(tile);

                            string insert_query = String.Format("INSERT INTO hex_grid (x, y, hex_type) VALUES ({0}, {1}, {2});", x, y, hexTileType);
                            using (MySqlCommand insert_command = new MySqlCommand(insert_query, connection))
                            {
                                insert_command.ExecuteNonQuery();
                            }
                        }
                    }
                    return hexGrid;
                }                
            });
            return await task;
        }

        public async static void SyncGrid(int id)
        {
            //TODO: Sync the requested Game Grid
            long accountID = Server.clients[id].account;            
            Data.HexGrid grid = await GetGridAsync();

            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.SYNC_GRID);
            string hexGrid = await Data.Serialize<Data.HexGrid>(grid);
            packet.Write(hexGrid);

            Sender.TCP_Send(id, packet);
        }

        private async static Task<Data.HexGrid> GetGridAsync()
        {
            Task<Data.HexGrid> task = Task.Run(() =>
            {
                Data.HexGrid hexGrid = new Data.HexGrid();

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string select_query = String.Format("SELECT x, y, hex_type FROM hex_grid;");
                    using (MySqlCommand select_command = new MySqlCommand(select_query, connection))
                    {
                        using (MySqlDataReader reader = select_command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    Data.HexTile tile = new Data.HexTile();

                                    tile.x = int.Parse(reader["x"].ToString());
                                    tile.y = int.Parse(reader["y"].ToString());
                                    tile.hexType = int.Parse(reader["hex_type"].ToString());

                                    hexGrid.hexTiles.Add(tile);
                                }
                            }
                        }
                    }
                }                
                return hexGrid;
            });
            return await task;
        }



        private static int GetRandomHexTile()
        {
            int[] dynamicWeights = new int[] { 70, 10, 10, 10 };
            int totalWeight = 0;
            foreach (int weight in dynamicWeights)
            {
                totalWeight += weight;
            }

            Random random = new Random();

            int randomIndex = random.Next(0, totalWeight);
            int sum = 0;

            for (int i = 0; i < dynamicWeights.Length; i++)
            {
                sum += dynamicWeights[i];
                if (randomIndex < sum)
                {                                   
                    if (i != 0)
                    {
                        int decreaseAmount = 2;
                        int increaseAmount = 1;

                        dynamicWeights[i] = Math.Max(1, dynamicWeights[i] - decreaseAmount);

                        for (int j = 1; j < dynamicWeights.Length; j++)
                        {
                            if (j != i)
                            {
                                dynamicWeights[j] += increaseAmount;
                            }
                        }
                    }
                    return i;
                }
            }

            return 0; // Should never happen unless weights are misconfigured
        }

        private async static Task<int> GetHexTileTypeAsync(int x_pos, int y_pos)
        {
            Task<int> task = Task.Run(() =>
            {

                int hexTileType = 0;

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string select_query = String.Format("SELECT hex_type FROM hex_grid WHERE x = {0} and y = {1};", x_pos, y_pos);
                    using (MySqlCommand select_command = new MySqlCommand(select_query, connection))
                    {
                        using (MySqlDataReader reader = select_command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    Data.HexTile tile = new Data.HexTile();

                                    hexTileType = int.Parse(reader["hex_type"].ToString());
                                }
                            }
                        }
                    }
                }
                
                return hexTileType;
            });
            return await task;
        }

        private async static Task<bool> UpdateHexTileTypeAsync(int x_pos, int y_pos, Terminal.HexType hexTileType)
        {
            Task<bool> task = Task.Run(() =>
            {

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    int hexType = (int)hexTileType;

                    string update_query = String.Format("UPDATE hex_grid SET hex_type = {0} WHERE x = {1} and y = {2};", hexType, x_pos, y_pos);
                    using (MySqlCommand update_command = new MySqlCommand(update_query, connection))
                    {
                        update_command.ExecuteNonQuery();
                    }
                    return true;
                }                

            });
            return await task;
        }



        private async static Task<bool> UpdateHasCastleAsync(long accountID, int hasCastle)
        {
            Task<bool> task = Task.Run(() =>
            {
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string update_query = String.Format("UPDATE accounts SET has_castle = {0} WHERE id = '{1}';", hasCastle, accountID);
                    using (MySqlCommand update_command = new MySqlCommand(update_query, connection))
                    {
                        update_command.ExecuteNonQuery();
                    }
                    return true;
                }               
            });
            return await task;
        }

        private static async Task<bool> UpdateCastleNeighboursAsync(Data.HexTile castleTile)
        {
            Task<bool> task = Task.Run(async () =>
           {

               using (MySqlConnection connection = GetMySqlConnection())
               {
                   List<Data.HexTile> neighbours = await Get2RingsOfNeighboursAsync(castleTile);
                   foreach (Data.HexTile neighbour in neighbours)
                   {
                       switch (neighbour.hexType)
                       {
                           case (int)Terminal.HexType.FREE_LAND:
                               await UpdateHexTileTypeAsync(neighbour.x, neighbour.y, Terminal.HexType.PLAYER_LAND);
                               break;
                           case (int)Terminal.HexType.FREE_MOUNTAIN:
                               await UpdateHexTileTypeAsync(neighbour.x, neighbour.y, Terminal.HexType.PLAYER_MOUNTAIN);
                               break;
                           case (int)Terminal.HexType.FREE_FOREST:
                               await UpdateHexTileTypeAsync(neighbour.x, neighbour.y, Terminal.HexType.PLAYER_FOREST);
                               break;
                           case (int)Terminal.HexType.FREE_CROPS:
                               await UpdateHexTileTypeAsync(neighbour.x, neighbour.y, Terminal.HexType.PLAYER_CROPS);
                               break;
                       }
                   }
                   return true;
               }               
           });
            return await task;
        }

        private static async Task<bool> UpdateArmyCampNeighboursAsync(Data.HexTile castleTile)
        {
            Task<bool> task = Task.Run(async () =>
            {

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    List<Data.HexTile> neighbours = await Get2RingsOfNeighboursAsync(castleTile);
                    foreach (Data.HexTile neighbour in neighbours)
                    {
                        switch (neighbour.hexType)
                        {
                            case (int)Terminal.HexType.FREE_LAND:
                                await UpdateHexTileTypeAsync(neighbour.x, neighbour.y, Terminal.HexType.PLAYER_LAND);
                                break;
                            case (int)Terminal.HexType.FREE_MOUNTAIN:
                                await UpdateHexTileTypeAsync(neighbour.x, neighbour.y, Terminal.HexType.PLAYER_MOUNTAIN);
                                break;
                            case (int)Terminal.HexType.FREE_FOREST:
                                await UpdateHexTileTypeAsync(neighbour.x, neighbour.y, Terminal.HexType.PLAYER_FOREST);
                                break;
                            case (int)Terminal.HexType.FREE_CROPS:
                                await UpdateHexTileTypeAsync(neighbour.x, neighbour.y, Terminal.HexType.PLAYER_CROPS);
                                break;
                        }
                    }                 
                    return true;
                }
            });
            return await task;
        }

        private static async Task<bool> FillGapsAsync()
        {
            Task<bool> task = Task.Run(async () =>
            {

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    Data.HexGrid grid = await GetGridAsync();

                    foreach(Data.HexTile tile in grid.hexTiles)
                    {
                        if(tile.hexType == (int)Terminal.HexType.FREE_LAND || tile.hexType == (int)Terminal.HexType.FREE_MOUNTAIN || tile.hexType == (int)Terminal.HexType.FREE_FOREST || tile.hexType == (int)Terminal.HexType.FREE_CROPS)
                        {
                            bool isGap = true;
                            List<Data.HexTile> neighbours = await GetNeighboursAsync(tile);

                            foreach (Data.HexTile neighbour in neighbours)
                            {

                                if (neighbour.hexType == (int)Terminal.HexType.FREE_LAND || neighbour.hexType == (int)Terminal.HexType.FREE_MOUNTAIN || neighbour.hexType == (int)Terminal.HexType.FREE_FOREST || neighbour.hexType == (int)Terminal.HexType.FREE_CROPS)
                                {
                                    isGap = false;
                                    break;
                                }
                            }

                            if (isGap)
                            {
                                switch (tile.hexType)
                                {
                                    case (int)Terminal.HexType.FREE_LAND:
                                        await UpdateHexTileTypeAsync(tile.x, tile.y, Terminal.HexType.PLAYER_LAND);
                                        break;
                                    case (int)Terminal.HexType.FREE_MOUNTAIN:
                                        await UpdateHexTileTypeAsync(tile.x, tile.y, Terminal.HexType.PLAYER_MOUNTAIN);
                                        break;
                                    case (int)Terminal.HexType.FREE_FOREST:
                                        await UpdateHexTileTypeAsync(tile.x, tile.y, Terminal.HexType.PLAYER_FOREST);
                                        break;
                                    case (int)Terminal.HexType.FREE_CROPS:
                                        await UpdateHexTileTypeAsync(tile.x, tile.y, Terminal.HexType.PLAYER_CROPS);
                                        break;
                                }
                            }
                        }                        
                    }                   
                    return true;
                }
            });
            return await task;
        }



        private async static Task<bool> UpdatePlayerResourcesAsync(long accountID, int gems, int gold, int stone, int wood, int food, int stoneProduction, int woodProduction, int foodProduction)
        {
            Task<bool> task = Task.Run(() =>
            {
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string update_query = String.Format("UPDATE accounts SET gems = {0}, gold = {1}, stone = {2}, wood = {3}, food = {4}, stone_production = {5}, wood_production = {6}, food_production = {7}  WHERE id = '{8}';", gems, gold, stone, wood, food, stoneProduction, woodProduction, foodProduction, accountID);
                    using (MySqlCommand update_command = new MySqlCommand(update_query, connection))
                    {
                        update_command.ExecuteNonQuery();
                    }
                    return true;
                }                
            });
            return await task;
        }

        private async static Task<bool> UpdateBuildingProductionAsync(int stone_per_second, int wood_per_second, int food_per_second, int x_pos, int y_pos)
        {
            Task<bool> task = Task.Run(() =>
            {
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string update_query = String.Format("UPDATE hex_grid SET stone_per_second = stone_per_second + {0}, wood_per_second = wood_per_second + {1}, food_per_second = food_per_second + {2}  WHERE x = {3} AND y={4};", stone_per_second, wood_per_second, food_per_second, x_pos, y_pos);
                    using (MySqlCommand update_command = new MySqlCommand(update_query, connection))
                    {
                        update_command.ExecuteNonQuery();
                    }
                    return true;
                }
               

            });
            return await task;
        }



        private async static void CollectResources()
        {
            await CollectResourcesAsync();
        }

        private async static Task<bool> CollectResourcesAsync()
        { 
            Task<bool> task = Task.Run(() =>
            {
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string update_query = String.Format("UPDATE accounts SET stone = stone + stone_production, wood = wood + wood_production, food = food + food_production;");
                    using (MySqlCommand update_command = new MySqlCommand(update_query, connection))
                    {
                        update_command.ExecuteNonQuery();
                    }
                    return true;
                }
            });
            return await task;
        }


        private static async Task<List<Data.HexTile>> GetNeighboursAsync(Data.HexTile centerTile)
        {
            Task<List<Data.HexTile>> task = Task.Run(async () =>
            {
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    Data.HexGrid hexGrid = await GetGridAsync();
                    List<Data.HexTile> neighbors = new List<Data.HexTile>();
                    Data.Vector2Int currentPosition;

                    Data.Vector2Int[] evenDirections =
                    {
                    new Data.Vector2Int(0, +1),
                    new Data.Vector2Int(+1, +1),
                    new Data.Vector2Int(+1, 0),
                    new Data.Vector2Int(+1, -1),
                    new Data.Vector2Int(0, -1),
                    new Data.Vector2Int(-1, 0),
                };

                    Data.Vector2Int[] oddDirections =
                    {
                    new Data.Vector2Int(-1, +1),
                    new Data.Vector2Int(0, +1),
                    new Data.Vector2Int(+1, 0),
                    new Data.Vector2Int(0, -1),
                    new Data.Vector2Int(-1, -1),
                    new Data.Vector2Int(-1, 0),
                };

                    var directions = centerTile.y % 2 != 0 ? evenDirections : oddDirections;

                    foreach (Data.Vector2Int direction in directions)
                    {
                        currentPosition = new Data.Vector2Int(centerTile.x, centerTile.y);
                        currentPosition += direction;
                        if (currentPosition.x >= 0 && currentPosition.x < hexGrid.columns && currentPosition.y >= 0 && currentPosition.y < hexGrid.rows)
                        {
                            Data.HexTile neighbor = new Data.HexTile();
                            neighbor.x = currentPosition.x;
                            neighbor.y = currentPosition.y;
                            neighbor.hexType = await GetHexTileTypeAsync(currentPosition.x, currentPosition.y);

                            if (neighbor != null)
                            {
                                neighbors.Add(neighbor);
                            }
                        }

                    }
                    return neighbors;
                }                
            });
            return await task;
        }

        private static async Task<List<Data.HexTile>> Get2RingsOfNeighboursAsync(Data.HexTile centerTile)
        {
            Task<List<Data.HexTile>> task = Task.Run(async () =>
            {

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    Data.HexGrid hexGrid = await GetGridAsync();
                    List<Data.HexTile> neighbors = new List<Data.HexTile>();
                    Data.Vector2Int currentPosition;

                    Data.Vector2Int[] evenDirections =
                    {
                    new Data.Vector2Int(0, +1),
                    new Data.Vector2Int(+1, +1),
                    new Data.Vector2Int(+1, 0),
                    new Data.Vector2Int(+1, -1),
                    new Data.Vector2Int(0, -1),
                    new Data.Vector2Int(-1, 0),
                    new Data.Vector2Int(0, +2),
                    new Data.Vector2Int(+1, +2),
                    new Data.Vector2Int(+2, +1),
                    new Data.Vector2Int(+2, 0),
                    new Data.Vector2Int(+2, -1),
                    new Data.Vector2Int(+1, -2),
                    new Data.Vector2Int(0, -2),
                    new Data.Vector2Int(-1, -2),
                    new Data.Vector2Int(-1, -1),
                    new Data.Vector2Int(-2, 0),
                    new Data.Vector2Int(-1, +1),
                    new Data.Vector2Int(-1, +2),
                };

                    Data.Vector2Int[] oddDirections =
                    {
                    new Data.Vector2Int(-1, +1),
                    new Data.Vector2Int(0, +1),
                    new Data.Vector2Int(+1, 0),
                    new Data.Vector2Int(0, -1),
                    new Data.Vector2Int(-1, -1),
                    new Data.Vector2Int(-1, 0),
                    new Data.Vector2Int(0, +2),
                    new Data.Vector2Int(+1, +2),
                    new Data.Vector2Int(+1, +1),
                    new Data.Vector2Int(2, 0),
                    new Data.Vector2Int(+1, -1),
                    new Data.Vector2Int(+1, -2),
                    new Data.Vector2Int(0, -2),
                    new Data.Vector2Int(-1, -2),
                    new Data.Vector2Int(-2, -1),
                    new Data.Vector2Int(-2, 0),
                    new Data.Vector2Int(-2, +1),
                    new Data.Vector2Int(-1, +2),
                };

                    var directions = centerTile.y % 2 != 0 ? evenDirections : oddDirections;

                    foreach (Data.Vector2Int direction in directions)
                    {
                        currentPosition = new Data.Vector2Int(centerTile.x, centerTile.y);
                        currentPosition += direction;
                        if (currentPosition.x >= 0 && currentPosition.x < hexGrid.columns && currentPosition.y >= 0 && currentPosition.y < hexGrid.rows)
                        {
                            Data.HexTile neighbor = new Data.HexTile();
                            neighbor.x = currentPosition.x;
                            neighbor.y = currentPosition.y;
                            neighbor.hexType = await GetHexTileTypeAsync(currentPosition.x, currentPosition.y);

                            if (neighbor != null)
                            {
                                neighbors.Add(neighbor);
                            }
                        }

                    }
                    return neighbors;
                }
            });
            return await task;
        }


        private async static Task<List<Data.Unit>> GetUnitsAsync(long accountID)
        {
            Task<List<Data.Unit>> task = Task.Run(() =>
            {
                List<Data.Unit> data = new List<Data.Unit>();

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string select_query = String.Format("SELECT units.id, units.global_id, units.level, units.trained, units.ready, units.trained_time, server_units.health, server_units.train_time, server_units.housing FROM units LEFT JOIN server_units ON units.global_id = server_units.global_id && units.level = server_units.level WHERE units.account_id = '{0}';", accountID);
                    using (MySqlCommand select_command = new MySqlCommand(select_query, connection))
                    {
                        using (MySqlDataReader reader = select_command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    Data.Unit unit = new Data.Unit();

                                    unit.id = (Data.UnitID)Enum.Parse(typeof(Data.UnitID), reader["global_id"].ToString());
                                    long.TryParse(reader["id"].ToString(), out unit.databaseID);
                                    int.TryParse(reader["level"].ToString(), out unit.level);
                                    int.TryParse(reader["health"].ToString(), out unit.health);
                                    int.TryParse(reader["housing"].ToString(), out unit.housing);
                                    int.TryParse(reader["train_time"].ToString(), out unit.trainTime);
                                    float.TryParse(reader["trained_time"].ToString(), out unit.trainedTime);

                                    int isTrue = 0;
                                    int.TryParse(reader["trained"].ToString(), out isTrue);
                                    unit.trained = isTrue > 0;

                                    isTrue = 0;
                                    int.TryParse(reader["ready"].ToString(), out isTrue);
                                    unit.ready = isTrue > 0;

                                    data.Add(unit);
                                }
                            }
                        }
                    }
                }
                return data;
            });
            return await task;
        }


        public async static void TrainUnit(int clientID, string unitGlobalID, int level, int armyCamp_x, int armyCamp_y)
        {
            long accountID = Server.clients[clientID].account;
            int result = await TrainUnitAsync(accountID, unitGlobalID, level, armyCamp_x, armyCamp_y);

            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.TRAIN);                    
            packet.Write(result);
            packet.Write(unitGlobalID);
            packet.Write(armyCamp_x);
            packet.Write(armyCamp_y);
            Sender.TCP_Send(clientID, packet);
        }

        private async static Task<int> TrainUnitAsync(long accountID, string unitGlobalID, int level, int armyCamp_x, int armyCamp_y)
        {
            Task<int> task = Task.Run(async () =>
            {
                int response = 0;
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    Data.ServerUnit unit = GetServerUnit(connection, unitGlobalID, level);
                    if(unit != null)
                    {
                        Data.ServerBuilding serverArmyCamp = await GetServerBuildingAsync("army_camp", 1);
                        Data.HexTile unitArmyCamp = await GetArmyCampDataAsync(armyCamp_x, armyCamp_y);
                        
                        int max_capacity = serverArmyCamp.max_capacity;
                        int armyCampCapacity = unitArmyCamp.capacity;

                        if(unit.housing + armyCampCapacity <= max_capacity)
                        {
                            Data.Player player = await GetPlayerDataAsync(accountID);

                            if(player.food >= unit.requiredFood)
                            {
                                await UpdatePlayerResourcesAsync(accountID, player.gems, player.gold, player.stone, player.wood, player.food - unit.requiredFood, player.stoneProduction, player.woodProduction, player.foodProduction);

                                string insertQuery = String.Format("INSERT INTO units (global_id, level, account_id, army_camp_x, army_camp_y) VALUES ('{0}', {1}, {2}, {3}, {4});", unitGlobalID, level, accountID, armyCamp_x, armyCamp_y);
                                using (MySqlCommand insertCommand = new MySqlCommand(insertQuery, connection))
                                {
                                    insertCommand.ExecuteNonQuery();
                                }

                                string updateQuery = String.Format("UPDATE hex_grid SET capacity = capacity + {0}  WHERE x = {1} AND y={2};", unit.housing, armyCamp_x, armyCamp_y);
                                using (MySqlCommand updateCommand = new MySqlCommand(updateQuery, connection))
                                {
                                    updateCommand.ExecuteNonQuery();
                                }

                                response = 3; //Train started
                            }
                            else
                            {
                                response = 2; // Not enough food
                            }
                        }
                        else
                        {
                            response = 1; //Not enough space in army camp
                        }
                        
                    }

                }
                return response;
            });
            return await task;
        }


        public async static void CancelTrainUnit(int clientID, string unitGlobalID, int armyCamp_x, int armyCamp_y)
        {
            long accountID = Server.clients[clientID].account;

            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.CANCEL_TRAIN);           
            int result = await CancelTrainUnitAsync(accountID, unitGlobalID, armyCamp_x, armyCamp_y);
            packet.Write(result);
            packet.Write(armyCamp_x);
            packet.Write(armyCamp_y);
            Sender.TCP_Send(clientID, packet);
        }

        private async static Task<int> CancelTrainUnitAsync(long accountID, string unitGlobalID, int armyCamp_x, int armyCamp_y)
        {
            Task<int> task = Task.Run(async () =>
            {
                int response = 0;
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string query = String.Format("DELETE FROM units WHERE global_id = '{0}' AND account_id = {1} AND x = {2} AND y = {3} AND ready <= 0", unitGlobalID, accountID, armyCamp_x, armyCamp_y);
                    using(MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        command.ExecuteNonQuery();
                        response = 1;
                    }  
                }
                return response;
            });
            return await task;
        }

        private static void GeneralUpdateUnitTraining(MySqlConnection connection, float deltaTime)
        {
            string query = String.Format("UPDATE units LEFT JOIN server_units ON units.global_id = server_units.global_id AND units.level = server_units.level SET trained = 1 AND ready = 1 WHERE units.trained_time >= server_units.train_time");
            using (MySqlCommand command = new MySqlCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }

            query = String.Format("UPDATE units AS t1 INNER JOIN (SELECT units.id FROM units LEFT JOIN server_units ON units.global_id = server_units.global_id AND units.level = server_units.level WHERE units.trained <= 0 AND units.trained_time < server_units.train_time GROUP BY units.account_id) t2 ON t1.id = t2.id SET trained_time = trained_time + {0}", deltaTime);
            using (MySqlCommand command = new MySqlCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }

            //query = String.Format("UPDATE units AS t1 INNER JOIN (SELECT units.id, (IFNULL(buildings.capacity, 0) - IFNULL(t.occupied, 0)) AS capacity, server_units.housing FROM units LEFT JOIN server_units ON units.global_id = server_units.global_id AND units.level = server_units.level LEFT JOIN (SELECT buildings.account_id, SUM(server_buildings.capacity) AS capacity FROM buildings LEFT JOIN server_buildings ON buildings.global_id = server_buildings.global_id AND buildings.level = server_buildings.level WHERE buildings.global_id = 'armycamp' AND buildings.level > 0 GROUP BY buildings.account_id) AS buildings ON units.account_id = buildings.account_id LEFT JOIN (SELECT units.account_id, SUM(server_units.housing) AS occupied FROM units LEFT JOIN server_units ON units.global_id = server_units.global_id AND units.level = server_units.level WHERE units.ready > 0 GROUP BY units.account_id) AS t ON units.account_id = t.account_id WHERE units.trained > 0 AND units.ready <= 0 GROUP BY units.account_id) t2 ON t1.id = t2.id SET ready = 1 WHERE housing <= capacity");
            //using (MySqlCommand command = new MySqlCommand(query, connection))
            //{
            //    command.ExecuteNonQuery();
        }



        private async static void UpdateUnitTraining(double  deltaTime)
        {
            await UpdateUnitTrainingAsync(deltaTime);
        }

        private async static Task<bool> UpdateUnitTrainingAsync(double deltaTime)
        {
            Task<bool> task = Task.Run(() =>
            {
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string query = String.Format("UPDATE units LEFT JOIN server_units ON units.global_id = server_units.global_id AND units.level = server_units.level SET units.trained = 1, units.ready = 1 WHERE units.trained_time >= server_units.train_time");
                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        command.ExecuteNonQuery();
                    }

                    query = String.Format("UPDATE units AS t1 INNER JOIN (SELECT units.id FROM units LEFT JOIN server_units ON units.global_id = server_units.global_id AND units.level = server_units.level WHERE units.trained <= 0 AND units.trained_time < server_units.train_time GROUP BY units.account_id) t2 ON t1.id = t2.id SET trained_time = trained_time + {0}", deltaTime);
                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
                return true;
            });
            return await task;
        }

        #endregion
    }
}