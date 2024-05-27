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
            if((DateTime.Now - collectTime).TotalSeconds >= 1)
            {
                collectTime = DateTime.Now;
                CollectResources();
            }
        }

        public async static void AuthenticatePlayer(int id, string device)
        {
            long account_id = await AuthenticatePlayerAsync(id, device);          
            Server.clients[id].device = device;
            Server.clients[id].account = account_id;

            await UpdateHasCastleAsync(device, 0);
            await UpdatePlayerResourcesAsync(device, 10, 100, 3000, 3000, 3000, 0, 0, 0);

            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.AUTH);
            packet.Write(account_id);
            Sender.TCP_Send(id, packet);
        }

        private async static Task<long> AuthenticatePlayerAsync(int id, string device)
        {
            Task<long> task = Task.Run(() =>
            {
                long account_id = 0;
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
                                    account_id = long.Parse(reader["id"].ToString());
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
                            account_id = insert_command.LastInsertedId;
                        }
                    }
                }              

                return account_id;
            });
            return await task;
        }


        public async static void GetPlayerData(int id, string device)
        {
            long accountID = Server.clients[id].account;
            Data.Player data_player = await GetPlayerDataAsync(id, device);
            
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.SYNC);
            string player = await Data.Serialize<Data.Player>(data_player);
            packet.Write(player);
     
            Sender.TCP_Send(id, packet);
        }

        private async static Task<Data.Player> GetPlayerDataAsync(int id, string device)
        {
            Task<Data.Player> task = Task.Run(() =>
            {
                Data.Player data = new Data.Player();

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string select_query = String.Format("SELECT id, gold, gems, stone, wood, food, stone_production, wood_production, food_production, has_castle FROM accounts WHERE device_id = '{0}';", device);
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


        public async static void BuildCastle(int id, string deviceID, int x_pos, int y_pos)
        {
            int result = 0;
            Data.Player player = await GetPlayerDataAsync(id, deviceID);
            if(player.hasCastle == false)
            {
                int selectedTileTyle = await GetHexTileTypeAsync(x_pos, y_pos);

                Data.HexTile castleTile = new Data.HexTile();
                castleTile.x = x_pos;
                castleTile.y = y_pos;
                castleTile.hexType = selectedTileTyle;

                if ((Terminal.HexType)selectedTileTyle == Terminal.HexType.FREE_LAND)
                {
                    await UpdateHasCastleAsync(deviceID, 1);                                       
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

        public async static void BuildStoneMine(int id, string deviceID, int x_pos, int y_pos)
        {
            int result = 0;
                                   
            int selectedTileTyle = await GetHexTileTypeAsync(x_pos, y_pos);

            Data.HexTile stoneMineTile = new Data.HexTile();
            stoneMineTile.x = x_pos;
            stoneMineTile.y = y_pos;
            stoneMineTile.hexType = selectedTileTyle;

            if ((Terminal.HexType)selectedTileTyle == Terminal.HexType.PLAYER_LAND)
            {
                Data.Player player = await GetPlayerDataAsync(id, deviceID);
                Data.ServerBuilding serverBuilding = await GetServerBuildingAsync("stone_mine", 1);

                //if(player.gold >= serverBuilding.requiredGold && player.stone >= serverBuilding.requiredStone && player.wood >= serverBuilding.requiredWood)

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
                    await UpdatePlayerResourcesAsync(deviceID, player.gems, player.gold - serverBuilding.requiredGold, player.stone - serverBuilding.requiredStone, player.wood - serverBuilding.requiredWood, player.food, player.stoneProduction + stonePerSecond, player.woodProduction, player.foodProduction);
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

        public async static void BuildSawmill(int id, string deviceID, int x_pos, int y_pos)
        {
            int result = 0;

            int selectedTileTyle = await GetHexTileTypeAsync(x_pos, y_pos);

            Data.HexTile sawmillTile = new Data.HexTile();
            sawmillTile.x = x_pos;
            sawmillTile.y = y_pos;
            sawmillTile.hexType = selectedTileTyle;

            if ((Terminal.HexType)selectedTileTyle == Terminal.HexType.PLAYER_LAND)
            {
                Data.Player player = await GetPlayerDataAsync(id, deviceID);
                Data.ServerBuilding serverBuilding = await GetServerBuildingAsync("sawmill", 1);

                //if(player.gold >= serverBuilding.requiredGold && player.stone >= serverBuilding.requiredStone && player.wood >= serverBuilding.requiredWood)

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
                    await UpdatePlayerResourcesAsync(deviceID, player.gems, player.gold - serverBuilding.requiredGold, player.stone - serverBuilding.requiredStone, player.wood - serverBuilding.requiredWood, player.food, player.stoneProduction, player.woodProduction + woodPerSecond, player.foodProduction);
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

        public async static void BuildFarm(int id, string deviceID, int x_pos, int y_pos)
        {
            int result = 0;

            int selectedTileTyle = await GetHexTileTypeAsync(x_pos, y_pos);

            Data.HexTile farmTile = new Data.HexTile();
            farmTile.x = x_pos;
            farmTile.y = y_pos;
            farmTile.hexType = selectedTileTyle;

            if ((Terminal.HexType)selectedTileTyle == Terminal.HexType.PLAYER_LAND)
            {
                Data.Player player = await GetPlayerDataAsync(id, deviceID);
                Data.ServerBuilding serverBuilding = await GetServerBuildingAsync("farm", 1);
                

                if (player.gold >= serverBuilding.requiredGold && player.stone >= serverBuilding.requiredStone && player.wood >= serverBuilding.requiredWood)
                {
                    await UpdateHexTileTypeAsync(x_pos, y_pos, Terminal.HexType.PLAYER_SAWMILL);

                    List<Data.HexTile> neighbours = await GetNeighboursAsync(farmTile);

                    int foodPerSecond = 0;
                    foreach (Data.HexTile neighbour in neighbours)
                    {
                        if (neighbour.hexType == (int)Terminal.HexType.PLAYER_CROPS)
                        {
                            foodPerSecond += serverBuilding.foodPerSecond;
                        }
                    }
                    await UpdatePlayerResourcesAsync(deviceID, player.gems, player.gold - serverBuilding.requiredGold, player.stone - serverBuilding.requiredStone, player.wood - serverBuilding.requiredWood, player.food, player.stoneProduction, player.woodProduction, player.foodProduction + foodPerSecond);
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

        public async static void BuildArmyCamp(int id, string deviceID, int x_pos, int y_pos)
        {
            int result = 0;

            int selectedTileTyle = await GetHexTileTypeAsync(x_pos, y_pos);

            Data.HexTile armyCampTile = new Data.HexTile();
            armyCampTile.x = x_pos;
            armyCampTile.y = y_pos;
            armyCampTile.hexType = selectedTileTyle;

            if ((Terminal.HexType)selectedTileTyle == Terminal.HexType.FREE_LAND)
            {
                Data.Player player = await GetPlayerDataAsync(id, deviceID);
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
                            await UpdatePlayerResourcesAsync(deviceID, player.gems, player.gold - serverBuilding.requiredGold, player.stone - serverBuilding.requiredStone, player.wood - serverBuilding.requiredWood, player.food, player.stoneProduction, player.woodProduction, player.foodProduction);
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
                    string select_query = String.Format("SELECT id, required_gold, required_wood, required_stone, stone_per_second, wood_per_second, food_per_second FROM server_buildings WHERE global_id = '{0}' AND level = {1};", buildingID, level);
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
                                }
                            }
                        }
                    }
                }                
                return data;
            });
            return await task;
        }


        public async static void GenerateNewGrid(int id, string deviceID)
        {

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

        public async static void SyncGrid(int id, string device)
        {
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


        private async static Task<bool> UpdateHasCastleAsync(string deviceID, int hasCastle)
        {
            Task<bool> task = Task.Run(() =>
            {
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string update_query = String.Format("UPDATE accounts SET has_castle = {0} WHERE device_id = '{1}';", hasCastle, deviceID);
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


        private async static Task<bool> UpdatePlayerResourcesAsync(string deviceID, int gems, int gold, int stone, int wood, int food, int stoneProduction, int woodProduction, int foodProduction)
        {
            Task<bool> task = Task.Run(() =>
            {
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string update_query = String.Format("UPDATE accounts SET gems = {0}, gold = {1}, stone = {2}, wood = {3}, food = {4}, stone_production = {5}, wood_production = {6}, food_production = {7}  WHERE device_id = '{8}';", gems, gold, stone, wood, food, stoneProduction, woodProduction, foodProduction, deviceID);
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

        #endregion
    }
}