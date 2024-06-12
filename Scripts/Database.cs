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
                UpdateUnitsCoords();
                GameMaker();
            }
        }

        public async static void AuthenticatePlayer(int id, string device)
        {
            Data.InitializationData auth = await AuthenticatePlayerAsync(device);          
            Server.clients[id].device = device;
            Server.clients[id].account = auth.accountID;

            string authData = await Data.Serialize<Data.InitializationData>(auth);

            await UpdateHasCastleAsync(auth.accountID, 0, 0, 0);
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

                    //string update_query = String.Format("UPDATE accounts SET is_online = 1, is_searching = 0, in_game = 0 WHERE id = {0};", initializationData.accountID);
                    //using(MySqlCommand update_command = new MySqlCommand(update_query, connection))
                    //{
                    //    update_command.ExecuteNonQuery();
                    //}
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
                    string select_query = String.Format("SELECT id, gold, gems, stone, wood, food, stone_production, wood_production, food_production, has_castle, is_online, is_searching, in_game, game_id, is_player_1, castle_x, castle_y FROM accounts WHERE id = '{0}';", accountID);
                    using (MySqlCommand select_command = new MySqlCommand(select_query, connection))
                    {
                        using (MySqlDataReader reader = select_command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    //data.id = long.Parse(reader["id"].ToString());
                                    data.gold = int.Parse(reader["gold"].ToString());
                                    data.gems = int.Parse(reader["gems"].ToString());
                                    data.stone = int.Parse(reader["stone"].ToString());
                                    data.wood = int.Parse(reader["wood"].ToString());
                                    data.food = int.Parse(reader["food"].ToString());
                                    data.stoneProduction = int.Parse(reader["stone_production"].ToString());
                                    data.woodProduction = int.Parse(reader["wood_production"].ToString());
                                    data.foodProduction = int.Parse(reader["food_production"].ToString());
                                    data.hasCastle = bool.Parse(reader["has_castle"].ToString());
                                    data.isOnline = int.Parse(reader["is_online"].ToString());
                                    data.isSearching = int.Parse(reader["is_searching"].ToString());
                                    data.inGame = int.Parse(reader["in_game"].ToString());
                                    data.gameID = long.Parse(reader["game_id"].ToString());
                                    data.isPlayer1 = int.Parse(reader["is_player_1"].ToString());
                                    data.castle_x = int.Parse(reader["castle_x"].ToString());
                                    data.castle_y = int.Parse(reader["castle_y"].ToString());

                                }
                            }
                        }
                    }
                    connection.Close();
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
                int selectedTileTyle = await GetHexTileTypeAsync(player.gameID, x_pos, y_pos);

                Data.HexTile castleTile = new Data.HexTile();
                castleTile.x = x_pos;
                castleTile.y = y_pos;
                castleTile.hexType = selectedTileTyle;

                if ((Terminal.HexType)selectedTileTyle == Terminal.HexType.FREE_LAND)
                {
                    List<Data.HexTile> neighbours = await Get2RingsOfNeighboursAsync(player.gameID, castleTile);

                    foreach(Data.HexTile neighbour in neighbours)
                    {
                        bool canBuild = true;
                        if(player.isPlayer1 == 1)
                        {
                            if (neighbour.hexType == (int)Terminal.HexType.PLAYER2_LAND || neighbour.hexType == (int)Terminal.HexType.PLAYER2_MOUNTAIN || neighbour.hexType == (int)Terminal.HexType.PLAYER2_FOREST || neighbour.hexType == (int)Terminal.HexType.PLAYER2_CROPS || neighbour.hexType == (int)Terminal.HexType.PLAYER2_STONE_MINE || neighbour.hexType == (int)Terminal.HexType.PLAYER2_SAWMILL || neighbour.hexType == (int)Terminal.HexType.PLAYER2_FARM || neighbour.hexType == (int)Terminal.HexType.PLAYER2_ARMY_CAMP)
                            {
                                canBuild = false;
                                break;
                            }
                               
                        }
                        else
                        {
                            if (neighbour.hexType == (int)Terminal.HexType.PLAYER1_LAND || neighbour.hexType == (int)Terminal.HexType.PLAYER1_MOUNTAIN || neighbour.hexType == (int)Terminal.HexType.PLAYER1_FOREST || neighbour.hexType == (int)Terminal.HexType.PLAYER1_CROPS || neighbour.hexType == (int)Terminal.HexType.PLAYER1_STONE_MINE || neighbour.hexType == (int)Terminal.HexType.PLAYER1_SAWMILL || neighbour.hexType == (int)Terminal.HexType.PLAYER1_FARM || neighbour.hexType == (int)Terminal.HexType.PLAYER1_ARMY_CAMP)
                            {
                                canBuild = false;
                                break;
                            }
                        }

                        if (canBuild)
                        {
                            await UpdateHasCastleAsync(accountID, 1, x_pos, y_pos);

                            if(player.isPlayer1 == 1)
                            {
                                await UpdateHexTileTypeAsync(player.gameID, x_pos, y_pos, Terminal.HexType.PLAYER1_CASTLE);
                                await UpdateCastleNeighboursAsync(player.gameID, player.isPlayer1, castleTile);
                            }
                            else
                            {
                                await UpdateHexTileTypeAsync(player.gameID, x_pos, y_pos, Terminal.HexType.PLAYER2_CASTLE);
                                await UpdateCastleNeighboursAsync(player.gameID, player.isPlayer1, castleTile);
                            }
                            
                            result = 1;
                        }
                        
                    }

                    
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

            Data.Player player = await GetPlayerDataAsync(accountID);

            int result = 0;
                                   
            int selectedTileTyle = await GetHexTileTypeAsync(player.gameID, x_pos, y_pos);

            Data.HexTile stoneMineTile = new Data.HexTile();
            stoneMineTile.x = x_pos;
            stoneMineTile.y = y_pos;
            stoneMineTile.hexType = selectedTileTyle;

            if(player.isPlayer1 == 1)
            {
                if ((Terminal.HexType)selectedTileTyle == Terminal.HexType.PLAYER1_LAND)
                {                   
                    Data.ServerBuilding serverBuilding = await GetServerBuildingAsync("stone_mine", 1);

                    if (player.gold >= serverBuilding.requiredGold && player.stone >= serverBuilding.requiredStone && player.wood >= serverBuilding.requiredWood)
                    {
                        await UpdateHexTileTypeAsync(player.gameID, x_pos, y_pos, Terminal.HexType.PLAYER1_STONE_MINE);

                        List<Data.HexTile> neighbours = await GetNeighboursAsync(player.gameID, stoneMineTile);

                        int stonePerSecond = 0;
                        foreach (Data.HexTile neighbour in neighbours)
                        {
                            if (neighbour.hexType == (int)Terminal.HexType.PLAYER1_MOUNTAIN)
                            {
                                stonePerSecond += serverBuilding.stonePerSecond;
                            }
                        }
                        await UpdatePlayerResourcesAsync(accountID, player.gems, player.gold - serverBuilding.requiredGold, player.stone - serverBuilding.requiredStone, player.wood - serverBuilding.requiredWood, player.food, player.stoneProduction + stonePerSecond, player.woodProduction, player.foodProduction);
                        await UpdateBuildingProductionAsync(player.gameID, stonePerSecond, 0, 0, x_pos, y_pos);

                        result = 1;
                    }
                    else
                    {
                        result = 2;
                    }

                }
                                                                                   
            }
            else
            {
                if ((Terminal.HexType)selectedTileTyle == Terminal.HexType.PLAYER2_LAND)
                {
                    Data.ServerBuilding serverBuilding = await GetServerBuildingAsync("stone_mine", 1);

                    if (player.gold >= serverBuilding.requiredGold && player.stone >= serverBuilding.requiredStone && player.wood >= serverBuilding.requiredWood)
                    {
                        await UpdateHexTileTypeAsync(player.gameID, x_pos, y_pos, Terminal.HexType.PLAYER2_STONE_MINE);

                        List<Data.HexTile> neighbours = await GetNeighboursAsync(player.gameID, stoneMineTile);

                        int stonePerSecond = 0;
                        foreach (Data.HexTile neighbour in neighbours)
                        {
                            if (neighbour.hexType == (int)Terminal.HexType.PLAYER2_MOUNTAIN)
                            {
                                stonePerSecond += serverBuilding.stonePerSecond;
                            }
                        }
                        await UpdatePlayerResourcesAsync(accountID, player.gems, player.gold - serverBuilding.requiredGold, player.stone - serverBuilding.requiredStone, player.wood - serverBuilding.requiredWood, player.food, player.stoneProduction + stonePerSecond, player.woodProduction, player.foodProduction);
                        await UpdateBuildingProductionAsync(player.gameID, stonePerSecond, 0, 0, x_pos, y_pos);

                        result = 1;
                    }
                    else
                    {
                        result = 2;
                    }

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

            Data.Player player = await GetPlayerDataAsync(accountID);

            int result = 0;

            int selectedTileTyle = await GetHexTileTypeAsync(player.gameID, x_pos, y_pos);

            Data.HexTile sawmillTile = new Data.HexTile();
            sawmillTile.x = x_pos;
            sawmillTile.y = y_pos;
            sawmillTile.hexType = selectedTileTyle;

            if(player.isPlayer1 == 1)
            {
                if ((Terminal.HexType)selectedTileTyle == Terminal.HexType.PLAYER1_LAND)
                {
                    Data.ServerBuilding serverBuilding = await GetServerBuildingAsync("sawmill", 1);


                    if (player.gold >= serverBuilding.requiredGold && player.stone >= serverBuilding.requiredStone && player.wood >= serverBuilding.requiredWood)
                    {
                        await UpdateHexTileTypeAsync(player.gameID, x_pos, y_pos, Terminal.HexType.PLAYER1_SAWMILL);

                        List<Data.HexTile> neighbours = await GetNeighboursAsync(player.gameID, sawmillTile);

                        int woodPerSecond = 0;
                        foreach (Data.HexTile neighbour in neighbours)
                        {
                            if (neighbour.hexType == (int)Terminal.HexType.PLAYER1_FOREST)
                            {
                                woodPerSecond += serverBuilding.woodPerSecond;
                            }
                        }
                        await UpdatePlayerResourcesAsync(accountID, player.gems, player.gold - serverBuilding.requiredGold, player.stone - serverBuilding.requiredStone, player.wood - serverBuilding.requiredWood, player.food, player.stoneProduction, player.woodProduction + woodPerSecond, player.foodProduction);
                        await UpdateBuildingProductionAsync(player.gameID, 0, woodPerSecond, 0, x_pos, y_pos);

                        result = 1;
                    }
                    else
                    {
                        result = 2;
                    }
                }           
            }
            else
            {
                if ((Terminal.HexType)selectedTileTyle == Terminal.HexType.PLAYER2_LAND)
                {
                    Data.ServerBuilding serverBuilding = await GetServerBuildingAsync("sawmill", 1);


                    if (player.gold >= serverBuilding.requiredGold && player.stone >= serverBuilding.requiredStone && player.wood >= serverBuilding.requiredWood)
                    {
                        await UpdateHexTileTypeAsync(player.gameID, x_pos, y_pos, Terminal.HexType.PLAYER2_SAWMILL);

                        List<Data.HexTile> neighbours = await GetNeighboursAsync(player.gameID, sawmillTile);

                        int woodPerSecond = 0;
                        foreach (Data.HexTile neighbour in neighbours)
                        {
                            if (neighbour.hexType == (int)Terminal.HexType.PLAYER2_FOREST)
                            {
                                woodPerSecond += serverBuilding.woodPerSecond;
                            }
                        }
                        await UpdatePlayerResourcesAsync(accountID, player.gems, player.gold - serverBuilding.requiredGold, player.stone - serverBuilding.requiredStone, player.wood - serverBuilding.requiredWood, player.food, player.stoneProduction, player.woodProduction + woodPerSecond, player.foodProduction);
                        await UpdateBuildingProductionAsync(player.gameID, 0, woodPerSecond, 0, x_pos, y_pos);

                        result = 1;
                    }
                    else
                    {
                        result = 2;
                    }
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

            Data.Player player = await GetPlayerDataAsync(accountID);

            int result = 0;

            int selectedTileTyle = await GetHexTileTypeAsync(player.gameID, x_pos, y_pos);

            Data.HexTile farmTile = new Data.HexTile();
            farmTile.x = x_pos;
            farmTile.y = y_pos;
            farmTile.hexType = selectedTileTyle;


            if(player.isPlayer1 == 1)
            {
                if ((Terminal.HexType)selectedTileTyle == Terminal.HexType.PLAYER1_LAND)
                {                   
                    Data.ServerBuilding serverBuilding = await GetServerBuildingAsync("farm", 1);

                    if (player.gold >= serverBuilding.requiredGold && player.stone >= serverBuilding.requiredStone && player.wood >= serverBuilding.requiredWood)
                    {
                        await UpdateHexTileTypeAsync(player.gameID, x_pos, y_pos, Terminal.HexType.PLAYER1_FARM);

                        List<Data.HexTile> neighbours = await GetNeighboursAsync(player.gameID, farmTile);

                        int foodPerSecond = 0;
                        foreach (Data.HexTile neighbour in neighbours)
                        {
                            if (neighbour.hexType == (int)Terminal.HexType.PLAYER1_CROPS)
                            {
                                foodPerSecond += serverBuilding.foodPerSecond;
                            }
                        }
                        await UpdatePlayerResourcesAsync(accountID, player.gems, player.gold - serverBuilding.requiredGold, player.stone - serverBuilding.requiredStone, player.wood - serverBuilding.requiredWood, player.food, player.stoneProduction, player.woodProduction, player.foodProduction + foodPerSecond);
                        await UpdateBuildingProductionAsync(player.gameID, 0, 0, foodPerSecond, x_pos, y_pos);

                        result = 1;
                    }
                    else
                    {
                        result = 2;
                    }
                }            
            }
            else
            {
                if ((Terminal.HexType)selectedTileTyle == Terminal.HexType.PLAYER2_LAND)
                {
                    Data.ServerBuilding serverBuilding = await GetServerBuildingAsync("farm", 1);

                    if (player.gold >= serverBuilding.requiredGold && player.stone >= serverBuilding.requiredStone && player.wood >= serverBuilding.requiredWood)
                    {
                        await UpdateHexTileTypeAsync(player.gameID, x_pos, y_pos, Terminal.HexType.PLAYER2_FARM);

                        List<Data.HexTile> neighbours = await GetNeighboursAsync(player.gameID, farmTile);

                        int foodPerSecond = 0;
                        foreach (Data.HexTile neighbour in neighbours)
                        {
                            if (neighbour.hexType == (int)Terminal.HexType.PLAYER2_CROPS)
                            {
                                foodPerSecond += serverBuilding.foodPerSecond;
                            }
                        }
                        await UpdatePlayerResourcesAsync(accountID, player.gems, player.gold - serverBuilding.requiredGold, player.stone - serverBuilding.requiredStone, player.wood - serverBuilding.requiredWood, player.food, player.stoneProduction, player.woodProduction, player.foodProduction + foodPerSecond);
                        await UpdateBuildingProductionAsync(player.gameID, 0, 0, foodPerSecond, x_pos, y_pos);

                        result = 1;
                    }
                    else
                    {
                        result = 2;
                    }
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

            Data.Player player = await GetPlayerDataAsync(accountID);

            int result = 0;

            int selectedTileTyle = await GetHexTileTypeAsync(player.gameID, x_pos, y_pos);

            Data.HexTile armyCampTile = new Data.HexTile();
            armyCampTile.x = x_pos;
            armyCampTile.y = y_pos;
            armyCampTile.hexType = selectedTileTyle;

            if ((Terminal.HexType)selectedTileTyle == Terminal.HexType.FREE_LAND)
            {                
                Data.ServerBuilding serverBuilding = await GetServerBuildingAsync("army_camp", 1);

                if(player.hasCastle == true)
                {
                    if (player.gold >= serverBuilding.requiredGold && player.stone >= serverBuilding.requiredStone && player.wood >= serverBuilding.requiredWood)
                    {             
                        List<Data.HexTile> neighbours = await Get2RingsOfNeighboursAsync(player.gameID, armyCampTile);

                        bool  isOverlaping = false;
                        bool isInRange = false;
                        
                        foreach (Data.HexTile neighbour in neighbours)
                        {
                            if(player.isPlayer1 == 1)
                            {
                                if (neighbour.hexType == (int)Terminal.HexType.PLAYER2_LAND || neighbour.hexType == (int)Terminal.HexType.PLAYER2_MOUNTAIN || neighbour.hexType == (int)Terminal.HexType.PLAYER2_FOREST || neighbour.hexType == (int)Terminal.HexType.PLAYER2_CROPS || neighbour.hexType == (int)Terminal.HexType.PLAYER2_CASTLE || neighbour.hexType == (int)Terminal.HexType.PLAYER2_STONE_MINE || neighbour.hexType == (int)Terminal.HexType.PLAYER2_SAWMILL || neighbour.hexType == (int)Terminal.HexType.PLAYER2_FARM || neighbour.hexType == (int)Terminal.HexType.PLAYER2_ARMY_CAMP)
                                {
                                    isOverlaping = true;
                                    break;
                                }
                                if (neighbour.hexType == (int)Terminal.HexType.PLAYER1_LAND || neighbour.hexType == (int)Terminal.HexType.PLAYER1_MOUNTAIN || neighbour.hexType == (int)Terminal.HexType.PLAYER1_FOREST || neighbour.hexType == (int)Terminal.HexType.PLAYER1_CROPS || neighbour.hexType == (int)Terminal.HexType.PLAYER1_CASTLE || neighbour.hexType == (int)Terminal.HexType.PLAYER1_STONE_MINE || neighbour.hexType == (int)Terminal.HexType.PLAYER1_SAWMILL || neighbour.hexType == (int)Terminal.HexType.PLAYER1_FARM || neighbour.hexType == (int)Terminal.HexType.PLAYER1_ARMY_CAMP)
                                {
                                    isInRange = true;                                    
                                }
                            }
                            else
                            {
                                if (neighbour.hexType == (int)Terminal.HexType.PLAYER2_LAND || neighbour.hexType == (int)Terminal.HexType.PLAYER2_MOUNTAIN || neighbour.hexType == (int)Terminal.HexType.PLAYER2_FOREST || neighbour.hexType == (int)Terminal.HexType.PLAYER2_CROPS || neighbour.hexType == (int)Terminal.HexType.PLAYER2_CASTLE || neighbour.hexType == (int)Terminal.HexType.PLAYER2_STONE_MINE || neighbour.hexType == (int)Terminal.HexType.PLAYER2_SAWMILL || neighbour.hexType == (int)Terminal.HexType.PLAYER2_FARM || neighbour.hexType == (int)Terminal.HexType.PLAYER2_ARMY_CAMP)
                                {
                                    isInRange = true;                                    
                                }
                                if (neighbour.hexType == (int)Terminal.HexType.PLAYER1_LAND || neighbour.hexType == (int)Terminal.HexType.PLAYER1_MOUNTAIN || neighbour.hexType == (int)Terminal.HexType.PLAYER1_FOREST || neighbour.hexType == (int)Terminal.HexType.PLAYER1_CROPS || neighbour.hexType == (int)Terminal.HexType.PLAYER1_CASTLE || neighbour.hexType == (int)Terminal.HexType.PLAYER1_STONE_MINE || neighbour.hexType == (int)Terminal.HexType.PLAYER1_SAWMILL || neighbour.hexType == (int)Terminal.HexType.PLAYER1_FARM || neighbour.hexType == (int)Terminal.HexType.PLAYER1_ARMY_CAMP)
                                {
                                    isOverlaping = true;
                                    break;
                                }
                            }

                            
                        }
                        if (isOverlaping)
                        {
                            result = 2;
                        }
                        else
                        {
                            if (isInRange)
                            {
                                if (player.isPlayer1 == 1)
                                {
                                    await UpdateHexTileTypeAsync(player.gameID, x_pos, y_pos, Terminal.HexType.PLAYER1_ARMY_CAMP);
                                }
                                else
                                {
                                    await UpdateHexTileTypeAsync(player.gameID, x_pos, y_pos, Terminal.HexType.PLAYER2_ARMY_CAMP);
                                }
                                await UpdateArmyCampNeighboursAsync(player.gameID, player.isPlayer1, armyCampTile);
                                await UpdatePlayerResourcesAsync(accountID, player.gems, player.gold - serverBuilding.requiredGold, player.stone - serverBuilding.requiredStone, player.wood - serverBuilding.requiredWood, player.food, player.stoneProduction, player.woodProduction, player.foodProduction);
                                result = 3;
                            }
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
                    connection.Close();
                }                
                return data;
            });
            return await task;
        }

        private async static Task<Data.HexTile> GetArmyCampDataAsync(long gameID, int x, int y)
        {
            Task<Data.HexTile> task = Task.Run(() =>
            {
                Data.HexTile data = new Data.HexTile();

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string select_query = String.Format("SELECT x, y, health, capacity FROM hex_grid WHERE game_id = {0} AND x = {1} AND y = {2};", gameID, x, y);
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
                    connection.Close();
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
    

        private async static Task<bool> GenerateGridAsync(long gameID)
        {
            Task<bool> task = Task.Run(() =>
            {
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string delete_query = String.Format("DELETE FROM hex_grid;");
                    using (MySqlCommand delete_grid_command = new MySqlCommand(delete_query, connection))
                    {
                        delete_grid_command.ExecuteNonQuery();
                    }
                    delete_query = String.Format("DELETE FROM units;");
                    using (MySqlCommand delete_units_command = new MySqlCommand(delete_query, connection))
                    {
                        delete_units_command.ExecuteNonQuery();
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

                            string insert_query = String.Format("INSERT INTO hex_grid (game_id, x, y, hex_type) VALUES ({0}, {1}, {2}, {3});",gameID, x, y, hexTileType);
                            using (MySqlCommand insert_command = new MySqlCommand(insert_query, connection))
                            {
                                insert_command.ExecuteNonQuery();
                            }
                        }
                    }
                    connection.Close();
                    return true;
                }                
            });
            return await task;
        }

        public async static void SyncGrid(int id)
        {
            long accountID = Server.clients[id].account;
            Data.Player player = await GetPlayerDataAsync(accountID);
            Data.HexGrid grid = await GetGridAsync(player.gameID);

            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.SYNC_GRID);
            string hexGrid = await Data.Serialize<Data.HexGrid>(grid);
            packet.Write(hexGrid);

            Sender.TCP_Send(id, packet);
        }

        private async static Task<Data.HexGrid> GetGridAsync(long gameID)
        {
            Task<Data.HexGrid> task = Task.Run(() =>
            {
                Data.HexGrid hexGrid = new Data.HexGrid();

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string select_query = String.Format("SELECT x, y, hex_type FROM hex_grid WHERE game_id = {0};", gameID);
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
                    connection.Close();
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

        private async static Task<int> GetHexTileTypeAsync(long gameID, int x_pos, int y_pos)
        {
            Task<int> task = Task.Run(() =>
            {

                int hexTileType = 0;

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string select_query = String.Format("SELECT hex_type FROM hex_grid WHERE game_id = {0} AND x = {1} AND y = {2};", gameID, x_pos, y_pos);
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
                    connection.Close();
                }
                
                return hexTileType;
            });
            return await task;
        }

        private async static Task<bool> UpdateHexTileTypeAsync(long gameID, int x_pos, int y_pos, Terminal.HexType hexTileType)
        {
            Task<bool> task = Task.Run(() =>
            {

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    int hexType = (int)hexTileType;

                    string update_query = String.Format("UPDATE hex_grid SET hex_type = {0} WHERE game_id = {1} AND x = {2} AND y = {3};", hexType, gameID, x_pos, y_pos);
                    using (MySqlCommand update_command = new MySqlCommand(update_query, connection))
                    {
                        update_command.ExecuteNonQuery();
                    }
                    connection.Close();
                    return true;
                }                

            });
            return await task;
        }



        private async static Task<bool> UpdateHasCastleAsync(long accountID, int hasCastle, int x_pos, int y_pos)
        {
            Task<bool> task = Task.Run(() =>
            {
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string update_query = String.Format("UPDATE accounts SET has_castle = {0}, castle_x = {2}, castle_y = {3} WHERE id = '{1}';", hasCastle, accountID, x_pos, y_pos);
                    using (MySqlCommand update_command = new MySqlCommand(update_query, connection))
                    {
                        update_command.ExecuteNonQuery();
                    }
                    connection.Close();
                    return true;
                }               
            });
            return await task;
        }

        private static async Task<bool> UpdateCastleNeighboursAsync(long gameID, int isPlayer1, Data.HexTile castleTile)
        {
            Task<bool> task = Task.Run(async () =>
           {

               using (MySqlConnection connection = GetMySqlConnection())
               {
                   List<Data.HexTile> neighbours = await Get2RingsOfNeighboursAsync(gameID, castleTile);
                   foreach (Data.HexTile neighbour in neighbours)
                   {
                       switch (isPlayer1)
                       {
                           case 1:
                               switch (neighbour.hexType)
                               {
                                   case (int)Terminal.HexType.FREE_LAND:
                                       await UpdateHexTileTypeAsync(gameID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER1_LAND);
                                       break;
                                   case (int)Terminal.HexType.FREE_MOUNTAIN:
                                       await UpdateHexTileTypeAsync(gameID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER1_MOUNTAIN);
                                       break;
                                   case (int)Terminal.HexType.FREE_FOREST:
                                       await UpdateHexTileTypeAsync(gameID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER1_FOREST);
                                       break;
                                   case (int)Terminal.HexType.FREE_CROPS:
                                       await UpdateHexTileTypeAsync(gameID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER1_CROPS);
                                       break;
                               }
                               break;

                            case 0:
                               switch (neighbour.hexType)
                               {
                                   case (int)Terminal.HexType.FREE_LAND:
                                       await UpdateHexTileTypeAsync(gameID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER2_LAND);
                                       break;
                                   case (int)Terminal.HexType.FREE_MOUNTAIN:
                                       await UpdateHexTileTypeAsync(gameID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER2_MOUNTAIN);
                                       break;
                                   case (int)Terminal.HexType.FREE_FOREST:
                                       await UpdateHexTileTypeAsync(gameID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER2_FOREST);
                                       break;
                                   case (int)Terminal.HexType.FREE_CROPS:
                                       await UpdateHexTileTypeAsync(gameID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER2_CROPS);
                                       break;
                               }
                               break;                            
                       }                       
                   }
                   connection.Close();
                   return true;
               }               
           });
            return await task;
        }

        private static async Task<bool> UpdateArmyCampNeighboursAsync(long gameID, int isPlayer1, Data.HexTile castleTile)
        {
            Task<bool> task = Task.Run(async () =>
            {

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    List<Data.HexTile> neighbours = await Get2RingsOfNeighboursAsync(gameID, castleTile);
                    foreach (Data.HexTile neighbour in neighbours)
                    {
                        switch (isPlayer1)
                        {
                            case 1:
                                switch (neighbour.hexType)
                                {
                                    case (int)Terminal.HexType.FREE_LAND:
                                        await UpdateHexTileTypeAsync(gameID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER1_LAND);
                                        break;
                                    case (int)Terminal.HexType.FREE_MOUNTAIN:
                                        await UpdateHexTileTypeAsync(gameID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER1_MOUNTAIN);
                                        break;
                                    case (int)Terminal.HexType.FREE_FOREST:
                                        await UpdateHexTileTypeAsync(gameID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER1_FOREST);
                                        break;
                                    case (int)Terminal.HexType.FREE_CROPS:
                                        await UpdateHexTileTypeAsync(gameID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER1_CROPS);
                                        break;
                                }
                                break;

                            case 0:
                                switch (neighbour.hexType)
                                {
                                    case (int)Terminal.HexType.FREE_LAND:
                                        await UpdateHexTileTypeAsync(gameID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER2_LAND);
                                        break;
                                    case (int)Terminal.HexType.FREE_MOUNTAIN:
                                        await UpdateHexTileTypeAsync(gameID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER2_MOUNTAIN);
                                        break;
                                    case (int)Terminal.HexType.FREE_FOREST:
                                        await UpdateHexTileTypeAsync(gameID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER2_FOREST);
                                        break;
                                    case (int)Terminal.HexType.FREE_CROPS:
                                        await UpdateHexTileTypeAsync(gameID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER2_CROPS);
                                        break;
                                }
                                break;
                        }
                    }                 
                    return true;
                    connection.Close();
                }
            });
            return await task;
        }

        //private static async Task<bool> FillGapsAsync()
        //{
        //    Task<bool> task = Task.Run(async () =>
        //    {

        //        using (MySqlConnection connection = GetMySqlConnection())
        //        {
        //            Data.HexGrid grid = await GetGridAsync();

        //            foreach(Data.HexTile tile in grid.hexTiles)
        //            {
        //                if(tile.hexType == (int)Terminal.HexType.FREE_LAND || tile.hexType == (int)Terminal.HexType.FREE_MOUNTAIN || tile.hexType == (int)Terminal.HexType.FREE_FOREST || tile.hexType == (int)Terminal.HexType.FREE_CROPS)
        //                {
        //                    bool isGap = true;
        //                    List<Data.HexTile> neighbours = await GetNeighboursAsync(tile);

        //                    foreach (Data.HexTile neighbour in neighbours)
        //                    {

        //                        if (neighbour.hexType == (int)Terminal.HexType.FREE_LAND || neighbour.hexType == (int)Terminal.HexType.FREE_MOUNTAIN || neighbour.hexType == (int)Terminal.HexType.FREE_FOREST || neighbour.hexType == (int)Terminal.HexType.FREE_CROPS)
        //                        {
        //                            isGap = false;
        //                            break;
        //                        }
        //                    }

        //                    if (isGap)
        //                    {
        //                        switch (tile.hexType)
        //                        {
        //                            case (int)Terminal.HexType.FREE_LAND:
        //                                await UpdateHexTileTypeAsync(tile.x, tile.y, Terminal.HexType.PLAYER_LAND);
        //                                break;
        //                            case (int)Terminal.HexType.FREE_MOUNTAIN:
        //                                await UpdateHexTileTypeAsync(tile.x, tile.y, Terminal.HexType.PLAYER_MOUNTAIN);
        //                                break;
        //                            case (int)Terminal.HexType.FREE_FOREST:
        //                                await UpdateHexTileTypeAsync(tile.x, tile.y, Terminal.HexType.PLAYER_FOREST);
        //                                break;
        //                            case (int)Terminal.HexType.FREE_CROPS:
        //                                await UpdateHexTileTypeAsync(tile.x, tile.y, Terminal.HexType.PLAYER_CROPS);
        //                                break;
        //                        }
        //                    }
        //                }                        
        //            }                   
        //            return true;
        //        }
        //    });
        //    return await task;
        //}



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
                    connection.Close();
                    return true;
                }                
            });
            return await task;
        }

        private async static Task<bool> UpdateBuildingProductionAsync(long gameID, int stone_per_second, int wood_per_second, int food_per_second, int x_pos, int y_pos)
        {
            Task<bool> task = Task.Run(() =>
            {
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string update_query = String.Format("UPDATE hex_grid SET stone_per_second = stone_per_second + {0}, wood_per_second = wood_per_second + {1}, food_per_second = food_per_second + {2}  WHERE game_id = {3} AND x = {4} AND y={5};", stone_per_second, wood_per_second, food_per_second, gameID, x_pos, y_pos);
                    using (MySqlCommand update_command = new MySqlCommand(update_query, connection))
                    {
                        update_command.ExecuteNonQuery();
                    }
                    connection.Close();
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
                    connection.Close();
                    return true;                    
                }
            });
            return await task;
        }


        private static async Task<List<Data.HexTile>> GetNeighboursAsync(long gameID, Data.HexTile centerTile)
        {
            Task<List<Data.HexTile>> task = Task.Run(async () =>
            {
               
                Data.HexGrid hexGrid = await GetGridAsync(gameID);
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
                        neighbor.hexType = await GetHexTileTypeAsync(gameID, currentPosition.x, currentPosition.y);

                        if (neighbor != null)
                        {
                            neighbors.Add(neighbor);
                        }
                    }

                }
                return neighbors;                              
            });
            return await task;
        }

        private static async Task<List<Data.HexTile>> Get2RingsOfNeighboursAsync(long gameID, Data.HexTile centerTile)
        {
            Task<List<Data.HexTile>> task = Task.Run(async () =>
            {

               
                Data.HexGrid hexGrid = await GetGridAsync(gameID);
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
                        neighbor.hexType = await GetHexTileTypeAsync(gameID, currentPosition.x, currentPosition.y);

                        if (neighbor != null)
                        {
                            neighbors.Add(neighbor);
                        }
                    }

                }
                return neighbors;                
            });
            return await task;
        }



        private async static Task<Data.Unit> GetUnitAsync(long unitDatabaseID)
        {
            Task<Data.Unit> task = Task.Run(async () =>
            {
                
                Data.Unit unit = new Data.Unit();

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string select_query = String.Format("SELECT units.id, units.global_id, units.level, units.game_id, units.trained, units.ready_player1, units.ready_player2, units.trained_time, units.army_camp_x, units.army_camp_y, units.current_x, units.current_y, units.target_x, units.target_y, units.is_player1_unit, units.path, server_units.health, server_units.train_time, server_units.housing FROM units LEFT JOIN server_units ON units.global_id = server_units.global_id && units.level = server_units.level WHERE units.id = '{0}';", unitDatabaseID);
                    using (MySqlCommand select_command = new MySqlCommand(select_query, connection))
                    {
                        using (MySqlDataReader reader = select_command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {                                    
                                    unit.id = (Data.UnitID)Enum.Parse(typeof(Data.UnitID), reader["global_id"].ToString());
                                    long.TryParse(reader["id"].ToString(), out unit.databaseID);
                                    int.TryParse(reader["level"].ToString(), out unit.level);
                                    int.TryParse(reader["game_id"].ToString(), out unit.gameID);
                                    int.TryParse(reader["health"].ToString(), out unit.health);
                                    int.TryParse(reader["housing"].ToString(), out unit.housing);
                                    int.TryParse(reader["train_time"].ToString(), out unit.trainTime);
                                    float.TryParse(reader["trained_time"].ToString(), out unit.trainedTime);
                                    int.TryParse(reader["army_camp_x"].ToString(), out unit.armyCamp_x);
                                    int.TryParse(reader["army_camp_y"].ToString(), out unit.armyCamp_y);
                                    int.TryParse(reader["current_x"].ToString(), out unit.current_x);
                                    int.TryParse(reader["current_y"].ToString(), out unit.current_y);
                                    int.TryParse(reader["target_x"].ToString(), out unit.target_x);
                                    int.TryParse(reader["target_y"].ToString(), out unit.target_y);
                                    unit.serializedPath = reader["path"].ToString();

                                    int isTrue = 0;
                                    int.TryParse(reader["trained"].ToString(), out isTrue);
                                    unit.trained = isTrue > 0;

                                    isTrue = 0;
                                    int.TryParse(reader["ready_player1"].ToString(), out isTrue);
                                    unit.ready_player1 = isTrue > 0;

                                    isTrue = 0;
                                    int.TryParse(reader["ready_player2"].ToString(), out isTrue);
                                    unit.ready_player2 = isTrue > 0;

                                    isTrue = 0;
                                    int.TryParse(reader["is_player1_unit"].ToString(), out isTrue);
                                    unit.isPlayer1Unit = isTrue > 0;
                                                                    
                                }
                            }
                        }
                    }
                    connection.Close();
                }
               
                return unit;
            });
            return await task;
        }

        private async static Task<List<Data.Unit>> GetUnitsAsync(long accountID)
        {
            Task<List<Data.Unit>> task = Task.Run(async () =>
            {
                Data.Player player = await GetPlayerDataAsync(accountID);
                List<Data.Unit> data = new List<Data.Unit>();

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string select_query = String.Format("SELECT units.id, units.global_id, units.level, units.game_id, units.trained, units.ready_player1, units.ready_player2, units.trained_time, units.army_camp_x, units.army_camp_y, units.current_x, units.current_y, units.target_x, units.target_y, units.path, units.is_player1_unit, server_units.health, server_units.train_time, server_units.housing FROM units LEFT JOIN server_units ON units.global_id = server_units.global_id && units.level = server_units.level WHERE units.game_id = '{0}';", player.gameID);
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
                                    int.TryParse(reader["game_id"].ToString(), out unit.gameID);
                                    int.TryParse(reader["health"].ToString(), out unit.health);
                                    int.TryParse(reader["housing"].ToString(), out unit.housing);
                                    int.TryParse(reader["train_time"].ToString(), out unit.trainTime);
                                    float.TryParse(reader["trained_time"].ToString(), out unit.trainedTime);
                                    int.TryParse(reader["army_camp_x"].ToString(), out unit.armyCamp_x);
                                    int.TryParse(reader["army_camp_y"].ToString(), out unit.armyCamp_y);
                                    int.TryParse(reader["current_x"].ToString(), out unit.current_x);
                                    int.TryParse(reader["current_y"].ToString(), out unit.current_y);
                                    int.TryParse(reader["target_x"].ToString(), out unit.target_x);
                                    int.TryParse(reader["target_y"].ToString(), out unit.target_y);
                                    unit.serializedPath = reader["path"].ToString();

                                    //DEBUG
                                    //Console.WriteLine("Path-ul uniatii venit din baza de date este: " + unit.path);


                                    int isTrue = 0;
                                    int.TryParse(reader["trained"].ToString(), out isTrue);
                                    unit.trained = isTrue > 0;

                                    isTrue = 0;
                                    int.TryParse(reader["ready_player1"].ToString(), out isTrue);
                                    unit.ready_player1 = isTrue > 0;

                                    isTrue = 0;
                                    int.TryParse(reader["ready_player2"].ToString(), out isTrue);
                                    unit.ready_player2 = isTrue > 0;

                                    isTrue = 0;
                                    int.TryParse(reader["is_player1_unit"].ToString(), out isTrue);
                                    unit.isPlayer1Unit = isTrue > 0;                                    

                                    data.Add(unit);
                                }
                            }
                        }
                    }
                    connection.Close();
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
            Sender.TCP_Send(clientID, packet);
        }

        private async static Task<int> TrainUnitAsync(long accountID, string unitGlobalID, int level, int armyCamp_x, int armyCamp_y)
        {
            Task<int> task = Task.Run(async () =>
            {
                int response = 0;

                Data.Player player = await GetPlayerDataAsync(accountID);

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    Data.ServerUnit unit = GetServerUnit(connection, unitGlobalID, level);
                    if(unit != null)
                    {
                        Data.ServerBuilding serverArmyCamp = await GetServerBuildingAsync("army_camp", 1);
                        Data.HexTile unitArmyCamp = await GetArmyCampDataAsync(player.gameID, armyCamp_x, armyCamp_y);
                        
                        int max_capacity = serverArmyCamp.max_capacity;
                        int armyCampCapacity = unitArmyCamp.capacity;

                        if(unit.housing + armyCampCapacity <= max_capacity)
                        {                            
                            if(player.food >= unit.requiredFood)
                            {
                                await UpdatePlayerResourcesAsync(accountID, player.gems, player.gold, player.stone, player.wood, player.food - unit.requiredFood, player.stoneProduction, player.woodProduction, player.foodProduction);

                                List<Data.HexTile> path = await FindPath(player.gameID, player.castle_x, player.castle_y, armyCamp_x, armyCamp_y);
                                string serializedPath = await Data.Serialize<List<Data.HexTile>>(path);
                                
                                string insertQuery;
                                if(player.isPlayer1 == 1)
                                {
                                    insertQuery = String.Format("INSERT INTO units (global_id, level, game_id, account_id, army_camp_x, army_camp_y, current_x, current_y, target_x, target_y, path, is_player1_unit) VALUES ('{0}', {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, '{10}', {11});", unitGlobalID, level, player.gameID, accountID, armyCamp_x, armyCamp_y, player.castle_x, player.castle_y, armyCamp_x, armyCamp_y, serializedPath, 1);
                                }
                                else
                                {
                                    insertQuery = String.Format("INSERT INTO units (global_id, level, game_id, account_id, army_camp_x, army_camp_y, current_x, current_y, target_x, target_y, path, is_player1_unit) VALUES ('{0}', {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, '{10}', {11});", unitGlobalID, level, player.gameID, accountID, armyCamp_x, armyCamp_y, player.castle_x, player.castle_y, armyCamp_x, armyCamp_y, serializedPath, 0);
                                }

                                using (MySqlCommand insertCommand = new MySqlCommand(insertQuery, connection))
                                {
                                    insertCommand.ExecuteNonQuery();
                                }

                                string updateQuery = String.Format("UPDATE hex_grid SET capacity = capacity + {0}  WHERE game_id = {1} AND x = {2} AND y={3};", unit.housing, player.gameID, armyCamp_x, armyCamp_y);
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
                    connection.Close();
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

                Data.Player player = await GetPlayerDataAsync(accountID);

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string query = String.Format("DELETE FROM units WHERE global_id = '{0}' AND account_id = {1} AND game_id = {2} AND x = {3} AND y = {4} AND ready <= 0", unitGlobalID, accountID, player.gameID, armyCamp_x, armyCamp_y);
                    using(MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        command.ExecuteNonQuery();
                        response = 1;
                    }
                    connection.Close();
                }
                return response;
            });
            return await task;
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
                    string query = String.Format("UPDATE units LEFT JOIN server_units ON units.global_id = server_units.global_id AND units.level = server_units.level SET units.trained = 1 WHERE units.trained_time >= server_units.train_time");
                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        command.ExecuteNonQuery();
                    }

                    query = String.Format("UPDATE units AS t1 INNER JOIN (SELECT units.id FROM units LEFT JOIN server_units ON units.global_id = server_units.global_id AND units.level = server_units.level WHERE units.trained <= 0 AND units.trained_time < server_units.train_time GROUP BY units.account_id) t2 ON t1.id = t2.id SET trained_time = trained_time + {0}", deltaTime);
                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                    connection.Close();
                }
                return true;
            });
            return await task;
        }
     

        private async static void UpdateUnitsCoords()
        {
            await UpdateUnitsCoordsAsync();            
        }

        private async static Task<bool> UpdateUnitsCoordsAsync()
        {
            Task<bool> task = Task.Run(() =>
            {                
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string query = String.Format("UPDATE units SET current_x = target_x, current_y = target_y WHERE ready_player1 = 1 AND ready_player2 = 1;");
                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                    connection.Close();
                }                
                return true;
            });
            return await task;
        }
       

        public async static void UpdateUnitReady(int clientID, long unitDatabaseID, int grid_x, int grid_y, int isPlayer1)
        {
            int result = await UpdateUnitReadyAsync(unitDatabaseID, grid_x, grid_y, isPlayer1);

            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.UNIT_READY);
            packet.Write(result);
            Sender.TCP_Send(clientID, packet);
        }

        private async static Task<int> UpdateUnitReadyAsync(long unitDatabaseID, int grid_x, int grid_y, int isPlayer1)
        {
            Task<int> task = Task.Run(async () =>
            {                
                int result = 0;
                Data.Unit unit = await GetUnitAsync(unitDatabaseID);

                if (grid_x == unit.target_x && grid_y == unit.target_y)
                {
                    if(isPlayer1 == 1)
                    {
                        using (MySqlConnection connection = GetMySqlConnection())
                        {
                            string query = String.Format("UPDATE units SET ready_player1 = 1 WHERE id = {0};", unitDatabaseID);
                            using (MySqlCommand updateCommand = new MySqlCommand(query, connection))
                            {
                                updateCommand.ExecuteNonQuery();
                            }
                            result = 2; //Unit is ready
                            connection.Close();
                        }
                    }
                    else
                    {
                        using (MySqlConnection connection = GetMySqlConnection())
                        {
                            string query = String.Format("UPDATE units SET ready_player2 = 1 WHERE id = {0};", unitDatabaseID);
                            using (MySqlCommand updateCommand = new MySqlCommand(query, connection))
                            {
                                updateCommand.ExecuteNonQuery();
                            }
                            result = 2; //Unit is ready
                            connection.Close();
                        }
                    }                    
                }
                else
                {
                    result = 1; // Unit is not at target coords
                }                
                return result;
            });
            return await task;
        }        


        public async static void StartSearching(int clientID)
        {
            long accountID = Server.clients[clientID].account;
            int result = await StartSearchingAsync(accountID);

            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.SEARCH);
            packet.Write(result);
            Sender.TCP_Send(clientID, packet);
        }

        private async static Task<int> StartSearchingAsync(long accountID)
        {
            Task<int> task = Task.Run(() =>
            {
                int result = 0;
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string update_query = String.Format("UPDATE accounts SET is_searching = 1 WHERE id = {0};", accountID);
                    using (MySqlCommand update_command = new MySqlCommand(update_query, connection))
                    {
                        update_command.ExecuteNonQuery();
                        result = 1;
                    }
                    connection.Close();
                }
                return result;
            });
            return await task;
        }

        public async static void CancelSearching(int clientID)
        {
            long accountID = Server.clients[clientID].account;
            int result = await CancelSearchingAsync(accountID);

            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.SEARCH);
            packet.Write(result);
            Sender.TCP_Send(clientID, packet);
        }

        private async static Task<int> CancelSearchingAsync(long accountID)
        {
            Task<int> task = Task.Run(() =>
            {
                int result = 0;
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string update_query = String.Format("UPDATE accounts SET is_searching = 0 WHERE id = {0};", accountID);
                    using (MySqlCommand update_command = new MySqlCommand(update_query, connection))
                    {
                        update_command.ExecuteNonQuery();
                        result = 1;
                    }
                    connection.Close();
                }
                return result;
            });
            return await task;
        }


        private async static void GameMaker()
        {
            await GameMakerAsync();
        }

        private async static Task<bool> GameMakerAsync()
        {
            Task<bool> task = Task.Run(async () =>
            {
                List<long> accounts = new List<long>();

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string select_query = String.Format("SELECT id FROM accounts WHERE is_searching = 1");
                    using (MySqlCommand select_command = new MySqlCommand(select_query, connection))
                    {
                        using (MySqlDataReader reader = select_command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    long accountID = long.Parse(reader["id"].ToString());

                                    accounts.Add(accountID);
                                }
                            }
                        }
                        
                    }
                    if (accounts.Count >= 2)
                    {
                        long gameID;
                        Random random = new Random();
                        int account1_index = random.Next(accounts.Count);
                        int account2_index = random.Next(accounts.Count);

                        if( account1_index == account2_index)
                        {
                            while (account1_index == account2_index)
                            {                               
                                account2_index = random.Next(accounts.Count);
                            }
                        }


                                            
                        string insert_query = String.Format("INSERT INTO games (player1_id, player2_id) VALUES ({0}, {1});", accounts[account1_index], accounts[account2_index]);
                        using (MySqlCommand insert_command = new MySqlCommand(insert_query, connection))
                        {
                            insert_command.ExecuteNonQuery();
                            gameID = insert_command.LastInsertedId;
                        }

                        string updatePlayer1_query = String.Format("UPDATE accounts SET is_searching = 0, in_game = 1, game_id = {0}, is_player_1 = 1 WHERE id = {1}", gameID, accounts[account1_index]);
                        using (MySqlCommand updatePlayer1_command = new MySqlCommand(updatePlayer1_query, connection))
                        {
                            updatePlayer1_command.ExecuteNonQuery();                            
                        }

                        string updatePlayer2_query = String.Format("UPDATE accounts SET is_searching = 0, in_game = 1, game_id = {0}, is_player_1 = 0 WHERE id = {1}", gameID, accounts[account2_index]);
                        using (MySqlCommand updatePlayer2_command = new MySqlCommand(updatePlayer2_query, connection))
                        {
                            updatePlayer2_command.ExecuteNonQuery();
                        }

                        await GenerateGridAsync(gameID);
                    }
                    connection.Close();
                }
                return true;
            });
            return await task;
        }


        private async static Task<List<Data.HexTile>> FindPath(long gameID, int startTile_x, int startTile_y, int targetTile_x, int targetTile_y)
        {
            Task<List<Data.HexTile>> task = Task.Run(async () =>
            {                
                Data.HexGrid hexGrid = await GetGridAsync(gameID);

                Data.PathNode[,] pathGrid = new Data.PathNode[hexGrid.rows, hexGrid.columns];                

                foreach(Data.HexTile hexTile in hexGrid.hexTiles)
                {
                    Data.PathNode pathNode = new Data.PathNode(hexTile);
                    pathGrid[hexTile.x, hexTile.y] = pathNode;                    
                }

                Data.PathNode startNode = pathGrid[startTile_x, startTile_y];
                Data.PathNode targetNode = pathGrid[targetTile_x, targetTile_y];

                List<Data.PathNode> openSet = new List<Data.PathNode>();
                HashSet<Data.PathNode> closedSet = new HashSet<Data.PathNode>();
                openSet.Add(startNode);

                while (openSet.Count > 0)
                {
                    Data.PathNode currentNode = openSet[0];
                    for (int i = 1; i < openSet.Count; i++)
                    {
                        if (openSet[i].FCost < currentNode.FCost || (openSet[i].FCost == currentNode.FCost && openSet[i].hCost < currentNode.hCost))
                        {
                            currentNode = openSet[i];
                        }
                    }

                    openSet.Remove(currentNode);
                    closedSet.Add(currentNode);

                    if (currentNode == targetNode)
                    {
                        return RetracePath(startNode, targetNode);
                    }

                    foreach (Data.PathNode neighbor in GetPathNeighbors(pathGrid, currentNode, hexGrid.rows, hexGrid.columns))
                    {
                        if (!neighbor.IsWalkable() || closedSet.Contains(neighbor))
                        {
                            continue;
                        }

                        int newCostToNeighbor = currentNode.gCost + GetDistance(currentNode, neighbor);
                        if (newCostToNeighbor < neighbor.gCost || !openSet.Contains(neighbor))
                        {
                            neighbor.gCost = newCostToNeighbor;
                            neighbor.hCost = GetDistance(neighbor, targetNode);
                            neighbor.cameFromNode = currentNode;

                            if (!openSet.Contains(neighbor))
                            {
                                openSet.Add(neighbor);
                            }
                        }
                    }
                }

                return new List<Data.HexTile>(); // Return an empty path if no path is found
            });
            return await task;
        }

        private static List<Data.PathNode> GetPathNeighbors(Data.PathNode[,] pathGrid, Data.PathNode node, int height, int width)
        {
            List<Data.PathNode> neighbors = new List<Data.PathNode>();
            int x = node.tile.x;
            int y = node.tile.y;
            Data.Vector2Int[] directions = y % 2 != 0 ? new Data.Vector2Int[]
            {
            new Data.Vector2Int(0, +1), new Data.Vector2Int(+1, +1), new Data.Vector2Int(+1, 0),
            new Data.Vector2Int(+1, -1), new Data.Vector2Int(0, -1), new Data.Vector2Int(-1, 0)
            } :
            new Data.Vector2Int[]
            {
            new Data.Vector2Int(-1, +1), new Data.Vector2Int(0, +1), new Data.Vector2Int(+1, 0),
            new Data.Vector2Int(0,-1), new Data.Vector2Int(-1, -1), new Data.Vector2Int(-1, 0),
            };

            foreach (var direction in directions)
            {
                int nx = x + direction.x;
                int ny = y + direction.y;
                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                {
                    neighbors.Add(pathGrid[nx, ny]);
                }
            }
            return neighbors;
        }

        private static int GetDistance(Data.PathNode nodeA, Data.PathNode nodeB)
        {
            int dx = Math.Abs(nodeA.tile.x - nodeB.tile.x);
            int dy = Math.Abs(nodeA.tile.y - nodeB.tile.y);
            return dy + Math.Max(0, (dx - dy) / 2);
        }

        private static List<Data.HexTile> RetracePath(Data.PathNode startNode, Data.PathNode endNode)
        {
            List<Data.HexTile> path = new List<Data.HexTile>();
            Data.PathNode currentNode = endNode;

            while (currentNode != startNode)
            {
                path.Add(currentNode.tile);
                currentNode = currentNode.cameFromNode;
            }
            path.Reverse();
            return path;
        }

        #endregion
    }
}