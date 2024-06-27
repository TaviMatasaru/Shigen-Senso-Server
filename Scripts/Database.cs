using System;
using MySql.Data.MySqlClient;
using System.Data;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

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

            if((DateTime.Now - collectTime).TotalSeconds >= 1f)
            {
                collectTime = DateTime.Now;
                CollectResources();
                UpdateUnitTraining(deltaTime);
                UpdateUnitsCoords();
                GameMaker();
                GameManager();
                BattleManager();
            }
        }

        public async static void LoginPlayer(int id, string device, string username, string password)
        {
            Data.InitializationData auth = await LoginPlayerAsync(device, username, password);

            if(auth.accountID != -2 && auth.accountID != -3)
            {
                Server.clients[id].device = device;
                Server.clients[id].account = auth.accountID;                
            }            

            string authData = await Data.Serialize<Data.InitializationData>(auth);                   

            Packet packet = new Packet();

            packet.Write((int)Terminal.RequestsID.LOGIN);
            packet.Write(authData);

            Sender.TCP_Send(id, packet);
        }

        private async static Task<Data.InitializationData> LoginPlayerAsync(string device, string username, string password)
        {
            Task<Data.InitializationData> task = Task.Run(() =>
            {
                Data.InitializationData initializationData = new Data.InitializationData();
                bool userFound = false;
                bool isOnline = false;

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string select_query = String.Format("SELECT id, username, is_online FROM accounts WHERE username = '{0}' AND password = '{1}';", username, password);                    
                    using (MySqlCommand select_command = new MySqlCommand(select_query, connection))
                    {
                        using (MySqlDataReader reader = select_command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    initializationData.accountID = long.Parse(reader["id"].ToString());
                                    initializationData.username = reader["username"].ToString();

                                    int isTrue = 0;
                                    int.TryParse(reader["is_online"].ToString(), out isTrue);
                                    isOnline = isTrue > 0;

                                    userFound = true;
                                }
                            }
                        }
                    }
                    if (!userFound)
                    {
                        initializationData.accountID = -2;                      
                    }
                    else
                    {
                        if (isOnline)
                        {
                            initializationData.accountID = -3;
                        }
                        else
                        {
                            string updateIsOnline_query= String.Format("UPDATE accounts SET is_online = 1 WHERE id = {0}", initializationData.accountID);
                            using (MySqlCommand updatePlayer1_command = new MySqlCommand(updateIsOnline_query, connection))
                            {
                                updatePlayer1_command.ExecuteNonQuery();
                            }
                        }
                    }
                    connection.Close();
                }              

                return initializationData;
            });
            return await task;
        }

        public async static void AutoLoginPlayer(int id, string device)
        {
            Data.InitializationData auth = await AutoLoginPlayerAsync(device);

            if (auth.accountID != -2 && auth.accountID != -3)
            {
                Server.clients[id].device = device;
                Server.clients[id].account = auth.accountID;
            }            

            string authData = await Data.Serialize<Data.InitializationData>(auth);            

            Packet packet = new Packet();

            packet.Write((int)Terminal.RequestsID.AUTO_LOGIN);
            packet.Write(authData);

            Sender.TCP_Send(id, packet);
        }

        private async static Task<Data.InitializationData> AutoLoginPlayerAsync(string device)
        {
            Task<Data.InitializationData> task = Task.Run(() =>
            {
                Data.InitializationData initializationData = new Data.InitializationData();
                bool userFound = false;
                bool isOnline = false;

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string select_query = String.Format("SELECT id, username, is_online FROM accounts WHERE device_id = '{0}';", device);
                    
                    using (MySqlCommand select_command = new MySqlCommand(select_query, connection))
                    {
                        using (MySqlDataReader reader = select_command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    initializationData.accountID = long.Parse(reader["id"].ToString());
                                    initializationData.username = reader["username"].ToString();                                    

                                    int isTrue = 0;
                                    int.TryParse(reader["is_online"].ToString(), out isTrue);
                                    isOnline = isTrue > 0;

                                    userFound = true;
                                }
                            }
                        }
                    }
                    if (userFound == false || isOnline == true)
                    {
                        initializationData.accountID = -2;
                    }
                    else
                    {
                        if (userFound == true && isOnline == false)
                        {
                            string updateIsOnline_query = String.Format("UPDATE accounts SET is_online = 1 WHERE id = {0}", initializationData.accountID);
                            using (MySqlCommand updatePlayer1_command = new MySqlCommand(updateIsOnline_query, connection))
                            {
                                updatePlayer1_command.ExecuteNonQuery();
                            }
                        }
                    }
                }

                return initializationData;
            });
            return await task;
        }

        public async static void RegisterPlayer(int id, string device, string username, string password)
        {
            Data.InitializationData auth = await RegisterPlayerAsync(device, username, password);

            if (auth.accountID != -2)
            {
                Server.clients[id].device = device;
                Server.clients[id].account = auth.accountID;
            }

            string authData = await Data.Serialize<Data.InitializationData>(auth);

            Packet packet = new Packet();

            packet.Write((int)Terminal.RequestsID.REGISTER);
            packet.Write(authData);

            Sender.TCP_Send(id, packet);
        }

        private async static Task<Data.InitializationData> RegisterPlayerAsync(string device, string username, string password)
        {
            Task<Data.InitializationData> task = Task.Run(() =>
            {
                Data.InitializationData initializationData = new Data.InitializationData();
                
                bool userFound = false;
                bool isOnline = false;

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string select_query = String.Format("SELECT id, username, is_online FROM accounts WHERE username = '{0}';", username, password);
                    using (MySqlCommand select_command = new MySqlCommand(select_query, connection))
                    {
                        using (MySqlDataReader reader = select_command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    initializationData.accountID = long.Parse(reader["id"].ToString());
                                    initializationData.username = reader["username"].ToString();

                                    int isTrue = 0;
                                    int.TryParse(reader["is_online"].ToString(), out isTrue);
                                    isOnline = isTrue > 0;

                                    userFound = true;
                                }
                            }
                        }
                    }
                    if (userFound)
                    {
                        initializationData.accountID = -2;
                    }
                    else
                    {
                        string insert_query = String.Format("INSERT INTO accounts (device_id, username, password) VALUES ('{0}', '{1}', '{2}');", device, username, password);
                        using (MySqlCommand insert_command = new MySqlCommand(insert_query, connection))
                        {
                            insert_command.ExecuteNonQuery();
                            initializationData.accountID = insert_command.LastInsertedId;                            
                        }

                        string updateIsOnline_query = String.Format("UPDATE accounts SET is_online = 1 WHERE id = {0}", initializationData.accountID);
                        using (MySqlCommand updatePlayer1_command = new MySqlCommand(updateIsOnline_query, connection))
                        {
                            updatePlayer1_command.ExecuteNonQuery();
                        }
                        initializationData.username = username;
                    }
                    connection.Close();
                }

                return initializationData;
            });
            return await task;
        }

        public async static void LogoutPlayer(int id, string username)
        {
            int response = await LogoutPlayerAsync(id, username);
                      

            Packet packet = new Packet();

            packet.Write((int)Terminal.RequestsID.LOGOUT);
            packet.Write(response);

            Sender.TCP_Send(id, packet);
        }

        private async static Task<int> LogoutPlayerAsync(int id, string username)
        {
            long accountID = Server.clients[id].account;
            Task<int> task = Task.Run(() =>
            {
                int response = 0;               

                using (MySqlConnection connection = GetMySqlConnection())
                {
                                 
                    string updateIsOnline_query = String.Format("UPDATE accounts SET is_online = 0, in_game = 0, device_id = 'none' WHERE id = {0} AND username = '{1}'", accountID, username);
                    using (MySqlCommand updatePlayer1_command = new MySqlCommand(updateIsOnline_query, connection))
                    {
                        updatePlayer1_command.ExecuteNonQuery();
                        response = 1;
                    }                                            
                    connection.Close();
                }

                return response;
            });
            return await task;
        }

        public async static void LogoutDisconnectedClient(int id)
        {
            await LogoutDisconnectedClientAsync(id);
        }

        private async static Task<bool> LogoutDisconnectedClientAsync(int id)
        {            
            Task<bool> task = Task.Run(() =>
            {
                long accountID = Server.clients[id].account;                
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    //TODO : Change query to not leave the match 
                    string updateIsOnline_query = String.Format("UPDATE accounts SET is_online = 0, in_game = 0, is_searching = 0, game_id = -1, stone = 0, wood = 0, food = 0, stone_production = 0, wood_production = 0, food_production = 0, has_castle = 0, castle_x = 0, castle_y = 0 WHERE id = {0};", accountID);

                    using (MySqlCommand updateIsOnline_command = new MySqlCommand(updateIsOnline_query, connection))
                    {
                        updateIsOnline_command.ExecuteNonQuery();                       
                    }
                    connection.Close();
                }

                return true ;
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
                                await UpdateHexTileTypeAsync(player.gameID, accountID, x_pos, y_pos, Terminal.HexType.PLAYER1_CASTLE);
                                await UpdateCastleNeighboursAsync(player.gameID, accountID, player.isPlayer1, castleTile);
                            }
                            else
                            {
                                await UpdateHexTileTypeAsync(player.gameID, accountID, x_pos, y_pos, Terminal.HexType.PLAYER2_CASTLE);
                                await UpdateCastleNeighboursAsync(player.gameID, accountID, player.isPlayer1, castleTile);
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
                        await UpdateHexTileTypeAsync(player.gameID, accountID, x_pos, y_pos, Terminal.HexType.PLAYER1_STONE_MINE);

                        List<Data.HexTile> neighbours = await GetNeighboursAsync(player.gameID, accountID, stoneMineTile);

                        int stonePerSecond = 0;
                        foreach (Data.HexTile neighbour in neighbours)
                        {
                            if (neighbour.hexType == (int)Terminal.HexType.PLAYER1_MOUNTAIN)
                            {
                                stonePerSecond += serverBuilding.stonePerSecond;
                            }
                        }
                        await UpdatePlayerResourcesAsync(accountID, player.gems, player.gold - serverBuilding.requiredGold, player.stone - serverBuilding.requiredStone, player.wood - serverBuilding.requiredWood, player.food);
                        await UpdateBuildingProductionAsync(player.gameID, stonePerSecond, 0, 0, x_pos, y_pos);
                        await UpdatePlayerStoneProductionAsync(accountID, stonePerSecond);

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
                        await UpdateHexTileTypeAsync(player.gameID, accountID, x_pos, y_pos, Terminal.HexType.PLAYER2_STONE_MINE);

                        List<Data.HexTile> neighbours = await GetNeighboursAsync(player.gameID, accountID, stoneMineTile);

                        int stonePerSecond = 0;
                        foreach (Data.HexTile neighbour in neighbours)
                        {
                            if (neighbour.hexType == (int)Terminal.HexType.PLAYER2_MOUNTAIN)
                            {
                                stonePerSecond += serverBuilding.stonePerSecond;
                            }
                        }
                        await UpdatePlayerResourcesAsync(accountID, player.gems, player.gold - serverBuilding.requiredGold, player.stone - serverBuilding.requiredStone, player.wood - serverBuilding.requiredWood, player.food);
                        await UpdateBuildingProductionAsync(player.gameID, stonePerSecond, 0, 0, x_pos, y_pos);
                        await UpdatePlayerStoneProductionAsync(accountID, stonePerSecond);

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
                        await UpdateHexTileTypeAsync(player.gameID, accountID, x_pos, y_pos, Terminal.HexType.PLAYER1_SAWMILL);

                        List<Data.HexTile> neighbours = await GetNeighboursAsync(player.gameID, accountID, sawmillTile);

                        int woodPerSecond = 0;
                        foreach (Data.HexTile neighbour in neighbours)
                        {
                            if (neighbour.hexType == (int)Terminal.HexType.PLAYER1_FOREST)
                            {
                                woodPerSecond += serverBuilding.woodPerSecond;
                            }
                        }
                        await UpdatePlayerResourcesAsync(accountID, player.gems, player.gold - serverBuilding.requiredGold, player.stone - serverBuilding.requiredStone, player.wood - serverBuilding.requiredWood, player.food);
                        await UpdateBuildingProductionAsync(player.gameID, 0, woodPerSecond, 0, x_pos, y_pos);
                        await UpdatePlayerWoodProductionAsync(accountID, woodPerSecond);

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
                        await UpdateHexTileTypeAsync(player.gameID, accountID, x_pos, y_pos, Terminal.HexType.PLAYER2_SAWMILL);

                        List<Data.HexTile> neighbours = await GetNeighboursAsync(player.gameID, accountID, sawmillTile);

                        int woodPerSecond = 0;
                        foreach (Data.HexTile neighbour in neighbours)
                        {
                            if (neighbour.hexType == (int)Terminal.HexType.PLAYER2_FOREST)
                            {
                                woodPerSecond += serverBuilding.woodPerSecond;
                            }
                        }
                        await UpdatePlayerResourcesAsync(accountID, player.gems, player.gold - serverBuilding.requiredGold, player.stone - serverBuilding.requiredStone, player.wood - serverBuilding.requiredWood, player.food);
                        await UpdateBuildingProductionAsync(player.gameID, 0, woodPerSecond, 0, x_pos, y_pos);
                        await UpdatePlayerWoodProductionAsync(accountID, woodPerSecond);

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
                        await UpdateHexTileTypeAsync(player.gameID, accountID, x_pos, y_pos, Terminal.HexType.PLAYER1_FARM);

                        List<Data.HexTile> neighbours = await GetNeighboursAsync(player.gameID, accountID, farmTile);

                        int foodPerSecond = 0;
                        foreach (Data.HexTile neighbour in neighbours)
                        {
                            if (neighbour.hexType == (int)Terminal.HexType.PLAYER1_CROPS)
                            {
                                foodPerSecond += serverBuilding.foodPerSecond;
                            }
                        }
                        await UpdatePlayerResourcesAsync(accountID, player.gems, player.gold - serverBuilding.requiredGold, player.stone - serverBuilding.requiredStone, player.wood - serverBuilding.requiredWood, player.food);
                        await UpdateBuildingProductionAsync(player.gameID, 0, 0, foodPerSecond, x_pos, y_pos);
                        await UpdatePlayerFoodProductionAsync(accountID, foodPerSecond);

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
                        await UpdateHexTileTypeAsync(player.gameID, accountID, x_pos, y_pos, Terminal.HexType.PLAYER2_FARM);

                        List<Data.HexTile> neighbours = await GetNeighboursAsync(player.gameID, accountID, farmTile);

                        int foodPerSecond = 0;
                        foreach (Data.HexTile neighbour in neighbours)
                        {
                            if (neighbour.hexType == (int)Terminal.HexType.PLAYER2_CROPS)
                            {
                                foodPerSecond += serverBuilding.foodPerSecond;
                            }
                        }
                        await UpdatePlayerResourcesAsync(accountID, player.gems, player.gold - serverBuilding.requiredGold, player.stone - serverBuilding.requiredStone, player.wood - serverBuilding.requiredWood, player.food);
                        await UpdateBuildingProductionAsync(player.gameID, 0, 0, foodPerSecond, x_pos, y_pos);
                        await UpdatePlayerFoodProductionAsync(accountID, foodPerSecond);

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
                                    await UpdateHexTileTypeAsync(player.gameID, accountID, x_pos, y_pos, Terminal.HexType.PLAYER1_ARMY_CAMP);
                                }
                                else
                                {
                                    await UpdateHexTileTypeAsync(player.gameID, accountID, x_pos, y_pos, Terminal.HexType.PLAYER2_ARMY_CAMP);
                                }
                                await UpdateArmyCampNeighboursAsync(player.gameID, accountID, player.isPlayer1, armyCampTile);
                                await UpdatePlayerResourcesAsync(accountID, player.gems, player.gold - serverBuilding.requiredGold, player.stone - serverBuilding.requiredStone, player.wood - serverBuilding.requiredWood, player.food);
                                await UpdateBuildingsAndPlayerProduction(accountID);
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
                    string select_query = String.Format("SELECT x, y, account_id, health, capacity, attack, defense, is_attacking, is_defending, is_under_attack, attacker_account_id FROM hex_grid WHERE game_id = {0} AND x = {1} AND y = {2};", gameID, x, y);
                    using (MySqlCommand select_command = new MySqlCommand(select_query, connection))
                    {
                        using (MySqlDataReader reader = select_command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    data.gameID = gameID;
                                    data.accountID = long.Parse(reader["account_id"].ToString());
                                    data.attackerAccountID = long.Parse(reader["attacker_account_id"].ToString());
                                    data.x = int.Parse(reader["x"].ToString());
                                    data.y = int.Parse(reader["y"].ToString());
                                    data.health = int.Parse(reader["health"].ToString());
                                    data.capacity = int.Parse(reader["capacity"].ToString());
                                    data.attack = int.Parse(reader["attack"].ToString());
                                    data.defense = int.Parse(reader["defense"].ToString());

                                    int isTrue = 0;
                                    int.TryParse(reader["is_attacking"].ToString(), out isTrue);
                                    data.isAttacking = isTrue > 0;

                                    isTrue = 0;
                                    int.TryParse(reader["is_defending"].ToString(), out isTrue);
                                    data.isDefending = isTrue > 0;

                                    isTrue = 0;
                                    int.TryParse(reader["is_under_attack"].ToString(), out isTrue);
                                    data.isUnderAttack = isTrue > 0;
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
            string query = String.Format("SELECT global_id, level, required_food, train_time, health, damage, def_damage, housing FROM server_units;");
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
                            int.TryParse(reader["damage"].ToString(), out unit.damage);
                            int.TryParse(reader["def_damage"].ToString(), out unit.def_damage);
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
            string query = String.Format("SELECT global_id, level, required_food, train_time, health, damage, def_damage, housing FROM server_units WHERE global_id = '{0}' AND level = {1};", unitID, level);
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
                            int.TryParse(reader["damage"].ToString(), out unit.damage);
                            int.TryParse(reader["def_damage"].ToString(), out unit.def_damage);
                            int.TryParse(reader["housing"].ToString(), out unit.housing);                           
                        }
                    }
                }
            }
            return unit;
        }



        private async static Task<bool> OLDGenerateGridAsync(long gameID)
        {
            Task<bool> task = Task.Run(() =>
            {
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    int[] dynamicWeights = new int[] { 70, 10, 10, 10 };
                    Data.HexGrid hexGrid = new Data.HexGrid();

                    for (int x = 0; x < hexGrid.rows; x++)
                    {
                        for (int y = 0; y < hexGrid.columns; y++)
                        {

                            int hexTileType = GetRandomHexTile(dynamicWeights);
                            Data.HexTile tile = new Data.HexTile();
                            tile.x = x;
                            tile.y = y;
                            tile.hexType = hexTileType;


                            //Data.HexTile tile = new Data.HexTile();
                            //tile.x = x;
                            //tile.y = y;

                            //if (x == 10 || y == 5)
                            //{
                            //    tile.hexType = 1;
                            //}
                            //else
                            //{
                            //    tile.hexType = hexTileType;
                            //}

                            hexGrid.hexTiles.Add(tile);

                            string insert_query = String.Format("INSERT INTO hex_grid (game_id, x, y, hex_type) VALUES ({0}, {1}, {2}, {3});", gameID, x, y, tile.hexType);
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

        private async static Task<bool> GenerateGridAsync(long gameID)
        {
            Task<bool> task = Task.Run(() =>
            {
                int[] dynamicWeights = new int[] { 70, 10, 10, 10 };
                Data.HexGrid hexGrid = new Data.HexGrid();

                Data.HexTile startingTile = new Data.HexTile();

                for (int x = 0; x < hexGrid.rows; x++)
                {
                    for (int y = 0; y < hexGrid.columns; y++)
                    {
                        int hexType = GetRandomHexTile(dynamicWeights);
                        Data.HexTile tile = new Data.HexTile();
                        tile.x = x;
                        tile.y = y;
                        tile.hexType = hexType;
                        hexGrid.hexTiles.Add(tile);

                        if(hexType == 0)
                        {
                            startingTile.x = x;
                            startingTile.y = y;
                            startingTile.hexType = hexType;
                            
                        }
                    }
                }

                bool isWalkable = true;
                do
                {
                    isWalkable = true;
                    foreach(Data.HexTile destinationTile in hexGrid.hexTiles)
                    {
                        if(destinationTile.hexType == 0 || destinationTile.hexType == 3)
                        {
                            Data.PathNode pathNode = FindPathOrClosestObstacle(hexGrid, startingTile.x, startingTile.y, destinationTile.x, destinationTile.y);
                            if (!pathNode.IsWalkable())
                            {
                                isWalkable = false;
                                ModifyHexTileType(hexGrid, pathNode.tile.x, pathNode.tile.y, 0);
                                
                                break;
                            }
                        }
                    }


                } while (!isWalkable);


                using (MySqlConnection connection = GetMySqlConnection())
                {
                    foreach(Data.HexTile tile in hexGrid.hexTiles)
                    {
                        string insert_query = String.Format("INSERT INTO hex_grid (game_id, x, y, hex_type) VALUES ({0}, {1}, {2}, {3});", gameID, tile.x, tile.y, tile.hexType);
                        using (MySqlCommand insert_command = new MySqlCommand(insert_query, connection))
                        {
                            insert_command.ExecuteNonQuery();
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
                    string select_query = String.Format("SELECT game_id, x, y, hex_type, stone_per_second, wood_per_second, food_per_second, health, capacity, attack, defense, is_attacking, is_defending, is_under_attack FROM hex_grid WHERE game_id = {0};", gameID);
                    using (MySqlCommand select_command = new MySqlCommand(select_query, connection))
                    {
                        using (MySqlDataReader reader = select_command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    Data.HexTile tile = new Data.HexTile();
                                    tile.gameID = long.Parse(reader["game_id"].ToString());
                                    tile.x = int.Parse(reader["x"].ToString());
                                    tile.y = int.Parse(reader["y"].ToString());
                                    tile.hexType = int.Parse(reader["hex_type"].ToString());
                                    tile.stonePerSecond = int.Parse(reader["stone_per_second"].ToString());
                                    tile.woodPerSecond = int.Parse(reader["wood_per_second"].ToString());
                                    tile.foodPerSecond = int.Parse(reader["food_per_second"].ToString());
                                    tile.health = int.Parse(reader["health"].ToString());
                                    tile.capacity = int.Parse(reader["capacity"].ToString());
                                    tile.attack = int.Parse(reader["attack"].ToString());
                                    tile.defense = int.Parse(reader["defense"].ToString());


                                    int isTrue = 0;
                                    int.TryParse(reader["is_attacking"].ToString(), out isTrue);
                                    tile.isAttacking = isTrue > 0;

                                    isTrue = 0;
                                    int.TryParse(reader["is_defending"].ToString(), out isTrue);
                                    tile.isDefending = isTrue > 0;

                                    isTrue = 0;
                                    int.TryParse(reader["is_under_attack"].ToString(), out isTrue);
                                    tile.isUnderAttack = isTrue > 0;


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


        public async static void SyncGame(int clientID, long gameID)
        {
            Data.GameData gameData = await GetGameAsync(gameID);
            Data.Player player1 = await GetPlayerDataAsync(gameData.player1AccountID);
            Data.Player player2 = await GetPlayerDataAsync(gameData.player2AccountID);

            Data.Game game = new Data.Game();
            game.player1_username = player1.username;
            game.player2_username = player2.username;
            game.player1_victories = player1.victories;
            game.player2_victories = player2.victories;
            game.player1_rank = player1.rank;
            game.player2_rank = player2.rank;
            game.gameData = gameData;

            String serialisedGame = await Data.Serialize<Data.Game>(game);

            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.SYNC_GAME);
            packet.Write(serialisedGame);
            Sender.TCP_Send(clientID, packet);

        }

        public async static Task<Data.GameData> GetGameAsync(long gameID)
        {
            Task<Data.GameData> task = Task.Run(() =>
            {
                Data.GameData gameData = new Data.GameData();

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string select_query = String.Format("SELECT id, player1_id, player2_id, game_result, player1_status, player2_status FROM games WHERE id = {0};", gameID);
                    using (MySqlCommand select_command = new MySqlCommand(select_query, connection))
                    {
                        using (MySqlDataReader reader = select_command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    long.TryParse(reader["id"].ToString(), out gameData.gameID);
                                    long.TryParse(reader["player1_id"].ToString(), out gameData.player1AccountID);
                                    long.TryParse(reader["player2_id"].ToString(), out gameData.player2AccountID);


                                    int gameResult = -1;
                                    int.TryParse(reader["game_result"].ToString(), out gameResult);
                                    gameData.gameResult = (Data.GameResultID)gameResult;

                                    int playerStatus = -1;
                                    int.TryParse(reader["player1_status"].ToString(), out playerStatus);
                                    gameData.player1Status = (Data.PlayerStatus)playerStatus;

                                    playerStatus = -1;
                                    int.TryParse(reader["player2_status"].ToString(), out playerStatus);
                                    gameData.player2Status = (Data.PlayerStatus)playerStatus;
                                }
                            }
                        }
                        connection.Close();
                    }
                }
                return gameData;
            });
            return await task;
        }



        private static int GetRandomHexTile(int[] dynamicWeights)
        {
            
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

        private static void ModifyHexTileType(Data.HexGrid grid, int targetX, int targetY, int newHexType)
        {
            foreach (Data.HexTile tile in grid.hexTiles)
            {
                if (tile.x == targetX && tile.y == targetY)
                {
                    tile.hexType = newHexType;
                    break;
                }
            }
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

        private async static Task<Data.HexTile> GetHexTileAsync(long gameID, long accountID, int x_pos, int y_pos)
        {
            Task<Data.HexTile> task = Task.Run(() =>
            {
                Data.HexTile tile = new Data.HexTile();

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string select_query = String.Format("SELECT game_id, account_id, x, y, hex_type, stone_per_second, wood_per_second, food_per_second,  health, capacity, attack, defense, is_attacking, is_defending, is_under_attack FROM hex_grid WHERE account_id = {0} AND game_id = {1} AND x = {2} AND y = {3};", accountID, gameID, x_pos, y_pos);
                    using (MySqlCommand select_command = new MySqlCommand(select_query, connection))
                    {
                        using (MySqlDataReader reader = select_command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {                                    
                                    tile.gameID = long.Parse(reader["game_id"].ToString());
                                    tile.accountID = long.Parse(reader["account_id"].ToString());
                                    tile.x = int.Parse(reader["x"].ToString());
                                    tile.y = int.Parse(reader["y"].ToString());
                                    tile.hexType = int.Parse(reader["hex_type"].ToString());
                                    tile.stonePerSecond = int.Parse(reader["stone_per_second"].ToString());
                                    tile.woodPerSecond = int.Parse(reader["wood_per_second"].ToString());
                                    tile.foodPerSecond = int.Parse(reader["food_per_second"].ToString());
                                    tile.health = int.Parse(reader["health"].ToString());
                                    tile.capacity = int.Parse(reader["capacity"].ToString());
                                    tile.attack = int.Parse(reader["attack"].ToString());
                                    tile.defense = int.Parse(reader["defense"].ToString());


                                    int isTrue = 0;
                                    int.TryParse(reader["is_attacking"].ToString(), out isTrue);
                                    tile.isAttacking = isTrue > 0;

                                    isTrue = 0;
                                    int.TryParse(reader["is_defending"].ToString(), out isTrue);
                                    tile.isDefending = isTrue > 0;

                                    isTrue = 0;
                                    int.TryParse(reader["is_under_attack"].ToString(), out isTrue);
                                    tile.isUnderAttack = isTrue > 0;
                                   
                                }
                            }
                        }
                    }
                    connection.Close();
                }              
    
                return tile;
            });
            return await task;
        }


        private async static Task<bool> UpdateHexTileTypeAsync(long gameID, long accountID, int x_pos, int y_pos, Terminal.HexType hexTileType)
        {
            Task<bool> task = Task.Run( () =>
            {

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    int hexType = (int)hexTileType;

                    string update_query = String.Format("UPDATE hex_grid SET hex_type = {0}, account_id = {1} WHERE game_id = {2} AND x = {3} AND y = {4};", hexType, accountID, gameID, x_pos, y_pos);
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

        private async static Task<bool> UpdateHexTileIsAttackingAsync(long gameID, long accountID, int x_pos, int y_pos, bool isAttacking)
        {
            Task<bool> task = Task.Run(() =>
            {

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    int is_attacking = 0;
                    if(isAttacking == true)
                    {
                        is_attacking = 1;
                    }
                    

                    string update_query = String.Format("UPDATE hex_grid SET is_attacking = {0} WHERE game_id = {1} AND x = {2} AND y = {3};", is_attacking, gameID, x_pos, y_pos);
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

        private async static Task<bool> UpdateHexTileIsDefendingAsync(long gameID, long accountID, int x_pos, int y_pos, bool isDefending)
        {
            Task<bool> task = Task.Run(() =>
            {

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    int is_defending = 0;
                    if (isDefending == true)
                    {
                        is_defending = 1;
                    }


                    string update_query = String.Format("UPDATE hex_grid SET is_defending = {0} WHERE game_id = {1} AND x = {2} AND y = {3};", is_defending, gameID, x_pos, y_pos);
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

        private async static Task<bool> UpdateHexTileIsUnderAttackAsync(long gameID, long accountID, int x_pos, int y_pos, bool isUnderAttack)
        {
            Task<bool> task = Task.Run(() =>
            {

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    int is_under_attack = 0;
                    if (isUnderAttack == true)
                    {
                        is_under_attack = 1;
                    }


                    string update_query = String.Format("UPDATE hex_grid SET is_under_attack = {0} WHERE game_id = {1} AND x = {2} AND y = {3};", is_under_attack, gameID, x_pos, y_pos);
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
                    string update_query = String.Format("UPDATE accounts SET has_castle = {0}, castle_x = {2}, castle_y = {3} WHERE id = {1};", hasCastle, accountID, x_pos, y_pos);
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

        private static async Task<bool> UpdateCastleNeighboursAsync(long gameID, long accountID, int isPlayer1, Data.HexTile castleTile)
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
                                       await UpdateHexTileTypeAsync(gameID, accountID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER1_LAND);
                                       break;
                                   case (int)Terminal.HexType.FREE_MOUNTAIN:
                                       await UpdateHexTileTypeAsync(gameID, accountID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER1_MOUNTAIN);
                                       break;
                                   case (int)Terminal.HexType.FREE_FOREST:
                                       await UpdateHexTileTypeAsync(gameID, accountID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER1_FOREST);
                                       break;
                                   case (int)Terminal.HexType.FREE_CROPS:
                                       await UpdateHexTileTypeAsync(gameID, accountID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER1_CROPS);
                                       break;
                               }
                               break;

                            case 0:
                               switch (neighbour.hexType)
                               {
                                   case (int)Terminal.HexType.FREE_LAND:
                                       await UpdateHexTileTypeAsync(gameID, accountID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER2_LAND);
                                       break;
                                   case (int)Terminal.HexType.FREE_MOUNTAIN:
                                       await UpdateHexTileTypeAsync(gameID, accountID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER2_MOUNTAIN);
                                       break;
                                   case (int)Terminal.HexType.FREE_FOREST:
                                       await UpdateHexTileTypeAsync(gameID, accountID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER2_FOREST);
                                       break;
                                   case (int)Terminal.HexType.FREE_CROPS:
                                       await UpdateHexTileTypeAsync(gameID, accountID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER2_CROPS);
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

        private static async Task<bool> UpdateArmyCampNeighboursAsync(long gameID, long accountID, int isPlayer1, Data.HexTile castleTile)
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
                                    case (int)Terminal.HexType.PLAYER2_LAND:
                                        await UpdateHexTileTypeAsync(gameID, accountID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER1_LAND);
                                        break;

                                    case (int)Terminal.HexType.FREE_MOUNTAIN:
                                    case (int)Terminal.HexType.PLAYER2_MOUNTAIN:
                                        await UpdateHexTileTypeAsync(gameID, accountID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER1_MOUNTAIN);
                                        break;

                                    case (int)Terminal.HexType.FREE_FOREST:
                                    case (int)Terminal.HexType.PLAYER2_FOREST:
                                        await UpdateHexTileTypeAsync(gameID, accountID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER1_FOREST);
                                        break;

                                    case (int)Terminal.HexType.FREE_CROPS:
                                    case (int)Terminal.HexType.PLAYER2_CROPS:
                                        await UpdateHexTileTypeAsync(gameID, accountID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER1_CROPS);
                                        break;

                                    case (int)Terminal.HexType.PLAYER2_STONE_MINE:
                                        await UpdateHexTileTypeAsync(gameID, accountID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER1_STONE_MINE);
                                        break;

                                    case (int)Terminal.HexType.PLAYER2_SAWMILL:
                                        await UpdateHexTileTypeAsync(gameID, accountID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER1_SAWMILL);
                                        break;

                                    case (int)Terminal.HexType.PLAYER2_FARM:
                                        await UpdateHexTileTypeAsync(gameID, accountID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER1_FARM);
                                        break;
                                }
                                break;

                            case 0:
                                switch (neighbour.hexType)
                                {
                                    case (int)Terminal.HexType.FREE_LAND:
                                    case (int)Terminal.HexType.PLAYER1_LAND:
                                        await UpdateHexTileTypeAsync(gameID, accountID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER2_LAND);
                                        break;

                                    case (int)Terminal.HexType.FREE_MOUNTAIN:
                                    case (int)Terminal.HexType.PLAYER1_MOUNTAIN:
                                        await UpdateHexTileTypeAsync(gameID, accountID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER2_MOUNTAIN);
                                        break;

                                    case (int)Terminal.HexType.FREE_FOREST:
                                    case (int)Terminal.HexType.PLAYER1_FOREST:
                                        await UpdateHexTileTypeAsync(gameID, accountID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER2_FOREST);
                                        break;

                                    case (int)Terminal.HexType.FREE_CROPS:
                                    case (int)Terminal.HexType.PLAYER1_CROPS:
                                        await UpdateHexTileTypeAsync(gameID, accountID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER2_CROPS);
                                        break;

                                    case (int)Terminal.HexType.PLAYER1_STONE_MINE:
                                        await UpdateHexTileTypeAsync(gameID, accountID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER2_STONE_MINE);
                                        break;

                                    case (int)Terminal.HexType.PLAYER1_SAWMILL:
                                        await UpdateHexTileTypeAsync(gameID, accountID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER2_SAWMILL);
                                        break;

                                    case (int)Terminal.HexType.PLAYER1_FARM:
                                        await UpdateHexTileTypeAsync(gameID, accountID, neighbour.x, neighbour.y, Terminal.HexType.PLAYER2_FARM);
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

        private static async Task<List<Data.HexTile>> GetNeighboursAsync(long gameID, long accountID, Data.HexTile centerTile)
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

                        Data.HexTile neighbour = await GetHexTileAsync(gameID, accountID, currentPosition.x, currentPosition.y);

                        if (neighbour != null)
                        {
                            neighbors.Add(neighbour);
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
       


        private async static Task<bool> UpdatePlayerResourcesAsync(long accountID, int gems, int gold, int stone, int wood, int food)
        {
            Task<bool> task = Task.Run(() =>
            {
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string update_query = String.Format("UPDATE accounts SET gems = {0}, gold = {1}, stone = {2}, wood = {3}, food = {4} WHERE id = {5};", gems, gold, stone, wood, food, accountID);
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

        private async static Task<bool> UpdatePlayerProductionAsync(long accountID, int stoneProduction, int woodProduction, int foodProduction)
        {
            Task<bool> task = Task.Run(() =>
            {
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string update_query = String.Format("UPDATE accounts SET stone_production = {0}, wood_production = {1}, food_production = {2}  WHERE id = {3};", stoneProduction, woodProduction, foodProduction, accountID);
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

        private async static Task<bool> UpdatePlayerStoneProductionAsync(long accountID, int stoneProduction)
        {
            Task<bool> task = Task.Run(() =>
            {
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string update_query = String.Format("UPDATE accounts SET stone_production = stone_production + {0} WHERE id = {1};", stoneProduction, accountID);
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

        private async static Task<bool> UpdatePlayerWoodProductionAsync(long accountID, int woodProduction)
        {
            Task<bool> task = Task.Run(() =>
            {
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string update_query = String.Format("UPDATE accounts SET wood_production = wood_production + {0} WHERE id = {1};", woodProduction, accountID);
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

        private async static Task<bool> UpdatePlayerFoodProductionAsync(long accountID, int foodProduction)
        {
            Task<bool> task = Task.Run(() =>
            {
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string update_query = String.Format("UPDATE accounts SET food_production = food_production + {0} WHERE id = {1};", foodProduction, accountID);
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

        private async static Task<bool> UpdateBuildingsAndPlayerProduction(long accountID)
        {
            Task<bool> task = Task.Run(async () =>
            {              
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    Data.ServerBuilding stoneMine = await GetServerBuildingAsync("stone_mine", 1);
                    Data.ServerBuilding sawmill = await GetServerBuildingAsync("sawmill", 1);
                    Data.ServerBuilding farm = await GetServerBuildingAsync("farm", 1);


                    List<Data.HexTile> playerBuildings = new List<Data.HexTile>();

                    string select_query = String.Format("SELECT game_id, account_id, x, y, hex_type, stone_per_second, wood_per_second, food_per_second,  health, capacity, attack, defense, is_attacking, is_defending, is_under_attack FROM hex_grid WHERE account_id = {0} AND (hex_type = 9 OR hex_type = 10 OR hex_type = 11 OR hex_type = 18 OR  hex_type = 19 OR hex_type = 20);", accountID);
                    using (MySqlCommand select_command = new MySqlCommand(select_query, connection))
                    {
                        using (MySqlDataReader reader = select_command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    Data.HexTile tile = new Data.HexTile();

                                    tile.gameID = long.Parse(reader["game_id"].ToString());
                                    tile.accountID = long.Parse(reader["account_id"].ToString());
                                    tile.x = int.Parse(reader["x"].ToString());
                                    tile.y = int.Parse(reader["y"].ToString());
                                    tile.hexType = int.Parse(reader["hex_type"].ToString());
                                    tile.stonePerSecond = int.Parse(reader["stone_per_second"].ToString());
                                    tile.woodPerSecond = int.Parse(reader["wood_per_second"].ToString());
                                    tile.foodPerSecond = int.Parse(reader["food_per_second"].ToString());
                                    tile.health = int.Parse(reader["health"].ToString());
                                    tile.capacity = int.Parse(reader["capacity"].ToString());
                                    tile.attack = int.Parse(reader["attack"].ToString());
                                    tile.defense = int.Parse(reader["defense"].ToString());


                                    int isTrue = 0;
                                    int.TryParse(reader["is_attacking"].ToString(), out isTrue);
                                    tile.isAttacking = isTrue > 0;

                                    isTrue = 0;
                                    int.TryParse(reader["is_defending"].ToString(), out isTrue);
                                    tile.isDefending = isTrue > 0;

                                    isTrue = 0;
                                    int.TryParse(reader["is_under_attack"].ToString(), out isTrue);
                                    tile.isUnderAttack = isTrue > 0;


                                    playerBuildings.Add(tile);
                                }
                            }
                        }
                    }                    

                    int playerStoneProduction = 0;
                    int playerWoodProduction = 0;
                    int playerFoodProduction = 0;

                    foreach (Data.HexTile playerBuilding in playerBuildings)
                    {
                        List<Data.HexTile> neighbours = await GetNeighboursAsync(playerBuilding.gameID, accountID, playerBuilding);

                        int buildingStoneProduction = 0;
                        int buildingWoodProduction = 0;
                        int buildingFoodProduction = 0;
                       

                        foreach (Data.HexTile neighbour in neighbours)
                        {
                            if((Terminal.HexType)playerBuilding.hexType == Terminal.HexType.PLAYER1_STONE_MINE || (Terminal.HexType)playerBuilding.hexType == Terminal.HexType.PLAYER2_STONE_MINE)
                            {
                                if((Terminal.HexType)neighbour.hexType == Terminal.HexType.PLAYER1_MOUNTAIN || (Terminal.HexType)neighbour.hexType == Terminal.HexType.PLAYER2_MOUNTAIN)
                                {
                                    if(playerBuilding.accountID == neighbour.accountID)
                                    {
                                        buildingStoneProduction += stoneMine.stonePerSecond;
                                    }
                                }
                            }

                            if ((Terminal.HexType)playerBuilding.hexType == Terminal.HexType.PLAYER1_SAWMILL || (Terminal.HexType)playerBuilding.hexType == Terminal.HexType.PLAYER2_SAWMILL)
                            {
                                if ((Terminal.HexType)neighbour.hexType == Terminal.HexType.PLAYER1_FOREST || (Terminal.HexType)neighbour.hexType == Terminal.HexType.PLAYER2_FOREST)
                                {
                                    if (playerBuilding.accountID == neighbour.accountID)
                                    {
                                        buildingWoodProduction += sawmill.woodPerSecond;
                                    }
                                }
                            }

                            if((Terminal.HexType)playerBuilding.hexType == Terminal.HexType.PLAYER1_FARM || (Terminal.HexType)playerBuilding.hexType == Terminal.HexType.PLAYER2_FARM)
                            {
                                if((Terminal.HexType)neighbour.hexType == Terminal.HexType.PLAYER1_CROPS || (Terminal.HexType)neighbour.hexType == Terminal.HexType.PLAYER2_CROPS)
                                {
                                    if(playerBuilding.accountID == neighbour.accountID)
                                    {
                                        buildingFoodProduction += farm.foodPerSecond;
                                    }
                                }
                            }
                            
                        }


                        await UpdateBuildingProductionAsync(playerBuilding.gameID, buildingStoneProduction, buildingWoodProduction, buildingFoodProduction, playerBuilding.x, playerBuilding.y);

                        playerStoneProduction += buildingStoneProduction;
                        playerWoodProduction += buildingWoodProduction;
                        playerFoodProduction += buildingFoodProduction;

                    }
                    
                    await UpdatePlayerProductionAsync(accountID, playerStoneProduction, playerWoodProduction, playerFoodProduction);

                    connection.Close();
                }
                return true;
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
        


        private async static Task<Data.Unit> GetUnitAsync(long unitDatabaseID)
        {
            Task<Data.Unit> task = Task.Run(async () =>
            {
                
                Data.Unit unit = new Data.Unit();

                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string select_query = String.Format("SELECT units.id, units.global_id, units.level, units.game_id, units.trained, units.ready_player1, units.ready_player2, units.trained_time, units.army_camp_x, units.army_camp_y, units.current_x, units.current_y, units.target_x, units.target_y, units.is_player1_unit, units.is_defending, units.path, server_units.health, server_units.damage, server_units.def_damage, server_units.train_time, server_units.housing FROM units LEFT JOIN server_units ON units.global_id = server_units.global_id && units.level = server_units.level WHERE units.id = '{0}';", unitDatabaseID);
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
                                    int.TryParse(reader["housing"].ToString(), out unit.housing);
                                    int.TryParse(reader["train_time"].ToString(), out unit.trainTime);
                                    float.TryParse(reader["trained_time"].ToString(), out unit.trainedTime);
                                    int.TryParse(reader["army_camp_x"].ToString(), out unit.armyCamp_x);
                                    int.TryParse(reader["army_camp_y"].ToString(), out unit.armyCamp_y);
                                    int.TryParse(reader["current_x"].ToString(), out unit.current_x);
                                    int.TryParse(reader["current_y"].ToString(), out unit.current_y);
                                    int.TryParse(reader["target_x"].ToString(), out unit.target_x);
                                    int.TryParse(reader["target_y"].ToString(), out unit.target_y);
                                    int.TryParse(reader["health"].ToString(), out unit.health);
                                    int.TryParse(reader["damage"].ToString(), out unit.damage);
                                    int.TryParse(reader["def_damage"].ToString(), out unit.def_damage);
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

                                    isTrue = 0;
                                    int.TryParse(reader["is_defending"].ToString(), out isTrue);
                                    unit.isDefending = isTrue > 0;

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
                    string select_query = String.Format("SELECT units.id, units.global_id, units.level, units.game_id, units.trained, units.ready_player1, units.ready_player2, units.trained_time, units.army_camp_x, units.army_camp_y, units.current_x, units.current_y, units.target_x, units.target_y, units.path, units.is_player1_unit, units.is_defending, server_units.health, server_units.damage, server_units.def_damage, server_units.train_time, server_units.housing FROM units LEFT JOIN server_units ON units.global_id = server_units.global_id && units.level = server_units.level WHERE units.game_id = '{0}';", player.gameID);
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
                                    int.TryParse(reader["housing"].ToString(), out unit.housing);
                                    int.TryParse(reader["train_time"].ToString(), out unit.trainTime);
                                    float.TryParse(reader["trained_time"].ToString(), out unit.trainedTime);
                                    int.TryParse(reader["army_camp_x"].ToString(), out unit.armyCamp_x);
                                    int.TryParse(reader["army_camp_y"].ToString(), out unit.armyCamp_y);
                                    int.TryParse(reader["current_x"].ToString(), out unit.current_x);
                                    int.TryParse(reader["current_y"].ToString(), out unit.current_y);
                                    int.TryParse(reader["target_x"].ToString(), out unit.target_x);
                                    int.TryParse(reader["target_y"].ToString(), out unit.target_y);
                                    int.TryParse(reader["health"].ToString(), out unit.health);
                                    int.TryParse(reader["damage"].ToString(), out unit.damage);
                                    int.TryParse(reader["def_damage"].ToString(), out unit.def_damage);
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

                                    isTrue = 0;
                                    int.TryParse(reader["is_defending"].ToString(), out isTrue);
                                    unit.isDefending = isTrue > 0;

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
                        Data.ServerBuilding serverArmyCamp = new Data.ServerBuilding();

                        if (armyCamp_x == player.castle_x && armyCamp_y == player.castle_y)
                        {
                            serverArmyCamp = await GetServerBuildingAsync("castle", 1);
                        }
                        else
                        {
                             serverArmyCamp = await GetServerBuildingAsync("army_camp", 1);
                        }

                        
                        Data.HexTile unitArmyCamp = await GetArmyCampDataAsync(player.gameID, armyCamp_x, armyCamp_y);
                        
                        int max_capacity = serverArmyCamp.max_capacity;
                        int armyCampCapacity = unitArmyCamp.capacity;

                        if(unitArmyCamp.isAttacking == false)
                        {
                            if (unit.housing + armyCampCapacity <= max_capacity)
                            {
                                if (player.food >= unit.requiredFood)
                                {
                                    await UpdatePlayerResourcesAsync(accountID, player.gems, player.gold, player.stone, player.wood, player.food - unit.requiredFood);

                                    string serializedPath = "";

                                    if(player.castle_x != armyCamp_x && player.castle_y != armyCamp_y)
                                    {
                                        List<Data.HexTile> path = await FindPath(player.gameID, player.castle_x, player.castle_y, armyCamp_x, armyCamp_y);
                                        serializedPath = await Data.Serialize<List<Data.HexTile>>(path);
                                    }
                                   
                                    string insertQuery;
                                    if (player.isPlayer1 == 1)
                                    {
                                        insertQuery = String.Format("INSERT INTO units (global_id, level, game_id, account_id, army_camp_x, army_camp_y, current_x, current_y, target_x, target_y, path, health, damage, def_damage, is_player1_unit) VALUES ('{0}', {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, '{10}', {11}, {12}, {13}, {14});", unitGlobalID, level, player.gameID, accountID, armyCamp_x, armyCamp_y, player.castle_x, player.castle_y, armyCamp_x, armyCamp_y, serializedPath, unit.health, unit.damage, unit.def_damage, 1);
                                    }
                                    else
                                    {
                                        insertQuery = String.Format("INSERT INTO units (global_id, level, game_id, account_id, army_camp_x, army_camp_y, current_x, current_y, target_x, target_y, path, health, damage, def_damage, is_player1_unit) VALUES ('{0}', {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, '{10}', {11}, {12}, {13}, {14});", unitGlobalID, level, player.gameID, accountID, armyCamp_x, armyCamp_y, player.castle_x, player.castle_y, armyCamp_x, armyCamp_y, serializedPath, unit.health, unit.damage, unit.def_damage, 0);
                                    }

                                    using (MySqlCommand insertCommand = new MySqlCommand(insertQuery, connection))
                                    {
                                        insertCommand.ExecuteNonQuery();
                                    }

                                    string updateQuery = String.Format("UPDATE hex_grid SET capacity = capacity + {0}, attack = attack + {1}, defense = defense + {2}  WHERE game_id = {3} AND x = {4} AND y={5};", unit.housing, unit.damage, unit.def_damage, player.gameID, armyCamp_x, armyCamp_y);
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
                        else
                        {
                            response = 0; // You can not train more units if your armycamp is attacking 
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

        private async static Task<bool> UpdateUnitArmyCampAsync(long gameID, long accountID, int army_camp_x, int army_camp_y, List<Data.Unit> units)
        {
            Task<bool> task = Task.Run(() =>
            {
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    foreach(Data.Unit unit in units)
                    {
                        string query = String.Format("UPDATE units SET army_camp_x = {0}, army_camp_y = {1} WHERE game_id = {2} AND account_id = {3} AND id = {4};", army_camp_x, army_camp_y, gameID, accountID, unit.databaseID);
                        using (MySqlCommand command = new MySqlCommand(query, connection))
                        {
                            command.ExecuteNonQuery();
                        }
                    }
                    
                    connection.Close();
                }
                return true;
            });
            return await task;
        }

        private async static Task<bool> UpdateArmyCampStatsAsync(long gameID, long accountID, int army_camp_x, int army_camp_y)
        {
            Task<bool> task = Task.Run(() =>
            {
                using (MySqlConnection connection = GetMySqlConnection())
                {                   
                    int capacity = 0;
                    int attack = 0;
                    int defense = 0;

                    string select_query = String.Format("SELECT units.id, units.global_id, units.level, units.game_id, units.trained, units.ready_player1, units.ready_player2, units.trained_time, units.army_camp_x, units.army_camp_y, units.current_x, units.current_y, units.target_x, units.target_y, units.path, units.is_player1_unit, units.is_defending, server_units.health, server_units.damage, server_units.def_damage, server_units.train_time, server_units.housing FROM units LEFT JOIN server_units ON units.global_id = server_units.global_id && units.level = server_units.level WHERE units.game_id = '{0}' AND units.army_camp_x = {1} AND units.army_camp_y = {2} AND units.account_id = {3};", gameID, army_camp_x, army_camp_y, accountID);
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
                                    int.TryParse(reader["housing"].ToString(), out unit.housing);
                                    int.TryParse(reader["train_time"].ToString(), out unit.trainTime);
                                    float.TryParse(reader["trained_time"].ToString(), out unit.trainedTime);
                                    int.TryParse(reader["army_camp_x"].ToString(), out unit.armyCamp_x);
                                    int.TryParse(reader["army_camp_y"].ToString(), out unit.armyCamp_y);
                                    int.TryParse(reader["current_x"].ToString(), out unit.current_x);
                                    int.TryParse(reader["current_y"].ToString(), out unit.current_y);
                                    int.TryParse(reader["target_x"].ToString(), out unit.target_x);
                                    int.TryParse(reader["target_y"].ToString(), out unit.target_y);
                                    int.TryParse(reader["health"].ToString(), out unit.health);
                                    int.TryParse(reader["damage"].ToString(), out unit.damage);
                                    int.TryParse(reader["def_damage"].ToString(), out unit.def_damage);
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

                                    isTrue = 0;
                                    int.TryParse(reader["is_defending"].ToString(), out isTrue);
                                    unit.isDefending = isTrue > 0;

                                    capacity += unit.housing;
                                    attack += unit.damage;
                                    defense += unit.def_damage;
                                   
                                }
                            }
                        }
                    }
                    
                    string query = String.Format("UPDATE hex_grid SET capacity = {0}, attack = {1}, defense = {2} WHERE game_id = {3} AND account_id = {4} AND x = {5} AND y = {6};", capacity, attack, defense, gameID, accountID, army_camp_x, army_camp_y);
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
                    string select_query = String.Format("SELECT id FROM accounts WHERE is_searching = 1 AND in_game = 0");
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


                                            
                        string insert_query = String.Format("INSERT INTO games (player1_id, player2_id, game_result, player1_status, player2_status) VALUES ({0}, {1}, {3}, {4}, {5});", accounts[account1_index], accounts[account2_index], (int)Data.GameResultID.NOT_OVER, Data.PlayerStatus.IN_GAME, Data.PlayerStatus.IN_GAME);
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


                        //TODO : Delete this 
                        //string delete_query = String.Format("DELETE FROM hex_grid;");
                        //using (MySqlCommand delete_grid_command = new MySqlCommand(delete_query, connection))
                        //{
                        //    delete_grid_command.ExecuteNonQuery();
                        //}
                        //delete_query = String.Format("DELETE FROM units;");
                        //using (MySqlCommand delete_units_command = new MySqlCommand(delete_query, connection))
                        //{
                        //    delete_units_command.ExecuteNonQuery();
                        //}

                        await UpdateHasCastleAsync(accounts[account1_index], 0, 0, 0);
                        await UpdatePlayerResourcesAsync(accounts[account1_index], 10, 100, 3000, 3000, 3000);

                        await UpdateHasCastleAsync(accounts[account2_index], 0, 0, 0);
                        await UpdatePlayerResourcesAsync(accounts[account2_index], 10, 100, 3000, 3000, 3000);

                        await GenerateGridAsync(gameID);
                    }
                    connection.Close();
                }
                return true;
            });
            return await task;
        }

        private async static void GameManager()
        {
            await GameManagerAsync();
        }

        private async static Task<bool> GameManagerAsync()
        {
            Task<bool> task = Task.Run(async () =>
            {                
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    List<Data.GameData> activeGames = new List<Data.GameData>();

                    string select_query = String.Format("SELECT id, player1_id, player2_id, game_result, player1_status, player2_status FROM games WHERE game_result = {0};", (int)Data.GameResultID.NOT_OVER);
                    using (MySqlCommand select_command = new MySqlCommand(select_query, connection))
                    {
                        using (MySqlDataReader reader = select_command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    Data.GameData game = new Data.GameData();
                                    
                                    long.TryParse(reader["id"].ToString(), out game.gameID);
                                    long.TryParse(reader["player1_id"].ToString(), out game.player1AccountID);
                                    long.TryParse(reader["player2_id"].ToString(), out game.player2AccountID);


                                    int gameResult = -1;
                                    int.TryParse(reader["game_result"].ToString(), out gameResult);
                                    game.gameResult = (Data.GameResultID)gameResult;

                                    int playerStatus = -1;
                                    int.TryParse(reader["player1_status"].ToString(), out playerStatus);
                                    game.player1Status = (Data.PlayerStatus)playerStatus;

                                    playerStatus = -1;
                                    int.TryParse(reader["player2_status"].ToString(), out playerStatus);
                                    game.player2Status = (Data.PlayerStatus)playerStatus;

                                    activeGames.Add(game);
                                }
                            }
                        }
                    }

                    if(activeGames.Count > 0)
                    {
                        foreach (Data.GameData game in activeGames)
                        {
                            Data.Player player1 = await GetPlayerDataAsync(game.player1AccountID);
                            Data.Player player2 = await GetPlayerDataAsync(game.player2AccountID);


                            if(player1.inGame == 0)
                            {
                                if(player1.isOnline == 1)
                                {
                                    string update_query = String.Format("UPDATE games SET game_result = {0}, player1_status = {1} WHERE id = {2};", (int)Data.GameResultID.P1_LEFT, (int)Data.PlayerStatus.LEFT, game.gameID);
                                    using (MySqlCommand update_command = new MySqlCommand(update_query, connection))
                                    {
                                        update_command.ExecuteNonQuery();
                                    }
                                }
                                else
                                {
                                    string update_query = String.Format("UPDATE games SET player1_status = {0} WHERE id = {1};", (int)Data.PlayerStatus.DISCONNECTED, game.gameID);
                                    using (MySqlCommand update_command = new MySqlCommand(update_query, connection))
                                    {
                                        update_command.ExecuteNonQuery();
                                    }
                                }
                            }

                            if (player2.inGame == 0)
                            {
                                if (player2.isOnline == 1)
                                {
                                    string update_query = String.Format("UPDATE games SET game_result = {0}, player2_status = {1} WHERE id = {2};", (int)Data.GameResultID.P2_LEFT, (int)Data.PlayerStatus.LEFT, game.gameID);
                                    using (MySqlCommand update_command = new MySqlCommand(update_query, connection))
                                    {
                                        update_command.ExecuteNonQuery();
                                    }
                                }
                                else
                                {
                                    string update_query = String.Format("UPDATE games SET player2_status = {0} WHERE id = {1};", (int)Data.PlayerStatus.DISCONNECTED, game.gameID);
                                    using (MySqlCommand update_command = new MySqlCommand(update_query, connection))
                                    {
                                        update_command.ExecuteNonQuery();
                                    }
                                }
                            }

                            if (player1.hasCastle && !player2.hasCastle)
                            {
                                string update_query = String.Format("UPDATE games SET game_result = {0} WHERE id = {1};", (int)Data.GameResultID.P1_WON, game.gameID);
                                using (MySqlCommand update_command = new MySqlCommand(update_query, connection))
                                {
                                    update_command.ExecuteNonQuery();
                                }
                            }

                            if(!player1.hasCastle && player2.hasCastle)
                            {
                                string update_query = String.Format("UPDATE games SET game_result = {0} WHERE id = {1};", (int)Data.GameResultID.P2_WON, game.gameID);
                                using (MySqlCommand update_command = new MySqlCommand(update_query, connection))
                                {
                                    update_command.ExecuteNonQuery();
                                }
                            }

                            if(game.player1Status == Data.PlayerStatus.LEFT && game.player2Status == Data.PlayerStatus.LEFT)
                            {
                                string delete_query = String.Format("DELETE from hex_grid WHERE id = {0};", game.gameID);
                                using (MySqlCommand delete_command = new MySqlCommand(delete_query, connection))
                                {
                                    delete_command.ExecuteNonQuery();
                                }

                                delete_query = String.Format("DELETE from units WHERE id = {0};", game.gameID);
                                using (MySqlCommand delete_command = new MySqlCommand(delete_query, connection))
                                {
                                    delete_command.ExecuteNonQuery();
                                }

                                activeGames.Remove(game);
                            }
                        }
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

        private static Data.PathNode FindPathOrClosestObstacle(Data.HexGrid hexGrid, int startTile_x, int startTile_y, int targetTile_x, int targetTile_y)
        {
            
            Data.PathNode[,] pathGrid = new Data.PathNode[hexGrid.rows, hexGrid.columns];

            foreach (Data.HexTile hexTile in hexGrid.hexTiles)
            {
                Data.PathNode pathNode = new Data.PathNode(hexTile);
                pathGrid[hexTile.x, hexTile.y] = pathNode;
            }

            Data.PathNode startNode = pathGrid[startTile_x, startTile_y];
            Data.PathNode targetNode = pathGrid[targetTile_x, targetTile_y];
            Data.PathNode closestObstacle = null;
            int closestDistance = int.MaxValue;

            List<Data.PathNode> openSet = new List<Data.PathNode> { startNode };
            HashSet<Data.PathNode> closedSet = new HashSet<Data.PathNode>();

            while (openSet.Count > 0)
            {
                Data.PathNode currentNode = openSet.OrderBy(node => node.FCost).ThenBy(node => node.hCost).First();
                openSet.Remove(currentNode);
                closedSet.Add(currentNode);

                if (currentNode == targetNode)
                {
                    return targetNode; // Path found
                }

                foreach (Data.PathNode neighbor in GetPathNeighbors(pathGrid, currentNode, hexGrid.rows, hexGrid.columns))
                {
                    if (!neighbor.IsWalkable() || closedSet.Contains(neighbor))
                    {
                        if (neighbor.tile.hexType == 1 || neighbor.tile.hexType == 2)
                        {
                            int distance = GetDistance(neighbor, targetNode);
                            if (distance < closestDistance)
                            {
                                closestObstacle = neighbor;
                                closestDistance = distance;
                            }
                        }
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

            return closestObstacle; // No path found, return closest obstacle
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



        public async static void LaunchAttack(int clientID, int attackingUnitsCount, int attackingArmyCamp_x, int attackingArmyCamp_y, int defendingArmyCamp_x, int defendingArmyCamp_y)
        {
            int response = await LaunchAttackAsync(clientID, attackingUnitsCount, attackingArmyCamp_x, attackingArmyCamp_y, defendingArmyCamp_x, defendingArmyCamp_y);

            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.LAUNCH_ATTACK);
            packet.Write(response);
            Sender.TCP_Send(clientID, response);
        }

        public async static Task<int> LaunchAttackAsync(int clientID, int attackingUnitsCount, int attackingArmyCamp_x, int attackingArmyCamp_y, int defendingArmyCamp_x, int defendingArmyCamp_y)
        {
            Task<int> task = Task.Run(async () =>
            {
                int result = 0;

                long accountID = Server.clients[clientID].account;
                Data.Player player = await GetPlayerDataAsync(accountID);
                Data.HexTile attackingArmyCamp = await GetArmyCampDataAsync(player.gameID, attackingArmyCamp_x, attackingArmyCamp_y);               

                if (attackingArmyCamp.isUnderAttack == false && attackingArmyCamp.isAttacking == false)
                {
                    Data.HexTile defendingArmyCamp = await GetArmyCampDataAsync(player.gameID, defendingArmyCamp_x, defendingArmyCamp_y);
                    if(defendingArmyCamp.isUnderAttack == false && defendingArmyCamp.isDefending == false)
                    {
                        using (MySqlConnection connection = GetMySqlConnection())
                        {
                            Data.ServerUnit barbarian = GetServerUnit(connection, "barbarian", 1);
                            
                            string update_query = String.Format("UPDATE hex_grid SET is_attacking = 1, capacity = capacity - {0}, attack = attack - {1}, defense = defense - {2} WHERE game_id = {3} AND x = {4} AND y = {5};", barbarian.housing * attackingUnitsCount, barbarian.damage * attackingUnitsCount, barbarian.def_damage * attackingUnitsCount, player.gameID, attackingArmyCamp_x, attackingArmyCamp_y);
                            using (MySqlCommand update_command = new MySqlCommand(update_query, connection))
                            {
                                update_command.ExecuteNonQuery();
                                
                            }

                            update_query = String.Format("UPDATE hex_grid SET is_defending = 1, attacker_account_id = {0} WHERE game_id = {1} AND x = {2} AND y = {3};", accountID, player.gameID, defendingArmyCamp_x, defendingArmyCamp_y);
                            using (MySqlCommand update_command = new MySqlCommand(update_query, connection))
                            {
                                update_command.ExecuteNonQuery();
                                result = 3;
                            }

                            List<Data.HexTile> path = await FindPath(player.gameID, attackingArmyCamp_x, attackingArmyCamp_y, defendingArmyCamp_x, defendingArmyCamp_y);
                            String serializedPath = await Data.Serialize<List<Data.HexTile>>(path);

                            update_query = String.Format("UPDATE units SET is_defending = 0, target_x = {0}, target_y = {1}, ready_player1 = 0, ready_player2 = 0, path = '{2}' WHERE game_id = {3} AND army_camp_x = {4} AND army_camp_y = {5} LIMIT {6};", defendingArmyCamp_x, defendingArmyCamp_y, serializedPath, player.gameID, attackingArmyCamp_x, attackingArmyCamp_y, attackingUnitsCount);
                            using (MySqlCommand update_command = new MySqlCommand(update_query, connection))
                            {
                                update_command.ExecuteNonQuery();                                
                            }

                            connection.Close();
                            result = 3; // Attack Lauched
                        }

                    }
                    else
                    {
                        result = 2; // Enemy ArmyCamp is already under attack
                    }
                }
                else
                {
                    result = 1; // You can't launch attack
                }


                return result;
            });
            return await task;

        }



        public async static void BattleManager()
        {
            await BattleManagerAsync();
        }

        public async static Task<bool> BattleManagerAsync()
        {
            Task<bool> task = Task.Run(async () =>
            {                
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    List<Data.HexTile> defendingArmyCamps = new List<Data.HexTile>();
                   
                    string select_query = String.Format("SELECT game_id, account_id, x, y, hex_type, health, capacity, attack, defense, is_attacking, is_defending, is_under_attack, attacker_account_id FROM hex_grid WHERE is_defending = 1;");
                    using (MySqlCommand select_command = new MySqlCommand(select_query, connection))
                    {
                        using (MySqlDataReader reader = select_command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    Data.HexTile tile = new Data.HexTile();

                                    tile.gameID = long.Parse(reader["game_id"].ToString());
                                    tile.accountID = long.Parse(reader["account_id"].ToString());
                                    tile.attackerAccountID = long.Parse(reader["attacker_account_id"].ToString());
                                    tile.x = int.Parse(reader["x"].ToString());
                                    tile.y = int.Parse(reader["y"].ToString());
                                    tile.hexType = int.Parse(reader["hex_type"].ToString());                                    
                                    tile.health = int.Parse(reader["health"].ToString());
                                    tile.capacity = int.Parse(reader["capacity"].ToString());
                                    tile.attack = int.Parse(reader["attack"].ToString());
                                    tile.defense = int.Parse(reader["defense"].ToString());


                                    int isTrue = 0;
                                    int.TryParse(reader["is_attacking"].ToString(), out isTrue);
                                    tile.isAttacking = isTrue > 0;

                                    isTrue = 0;
                                    int.TryParse(reader["is_defending"].ToString(), out isTrue);
                                    tile.isDefending = isTrue > 0;

                                    isTrue = 0;
                                    int.TryParse(reader["is_under_attack"].ToString(), out isTrue);
                                    tile.isUnderAttack = isTrue > 0;


                                    defendingArmyCamps.Add(tile);
                                }
                            }
                        }
                    }

                    if(defendingArmyCamps.Count > 0)
                    {
                        foreach (Data.HexTile defendingArmyCamp in defendingArmyCamps)
                        {
                            List<Data.Unit> attackingUnits = new List<Data.Unit>();

                            string selectUnits_query = String.Format("SELECT id, game_id, account_id, army_camp_x, army_camp_y, current_x, current_y, target_x, target_y, is_player1_unit, health, damage, def_damage, is_defending FROM units WHERE game_id = {0} AND target_x = {1} AND target_y = {2} AND is_defending = 0;", defendingArmyCamp.gameID, defendingArmyCamp.x, defendingArmyCamp.y);
                            using (MySqlCommand selectUnits_command = new MySqlCommand(selectUnits_query, connection))
                            {
                                using (MySqlDataReader reader = selectUnits_command.ExecuteReader())
                                {
                                    if (reader.HasRows)
                                    {
                                        while (reader.Read())
                                        {
                                            Data.Unit unit = new Data.Unit();

                                            long.TryParse(reader["id"].ToString(), out unit.databaseID);
                                            long.TryParse(reader["account_id"].ToString(), out unit.accountID);                                            
                                            int.TryParse(reader["game_id"].ToString(), out unit.gameID);
                                            int.TryParse(reader["army_camp_x"].ToString(), out unit.armyCamp_x);
                                            int.TryParse(reader["army_camp_y"].ToString(), out unit.armyCamp_y);
                                            int.TryParse(reader["current_x"].ToString(), out unit.current_x);
                                            int.TryParse(reader["current_y"].ToString(), out unit.current_y);
                                            int.TryParse(reader["target_x"].ToString(), out unit.target_x);
                                            int.TryParse(reader["target_y"].ToString(), out unit.target_y);
                                            int.TryParse(reader["health"].ToString(), out unit.health);
                                            int.TryParse(reader["damage"].ToString(), out unit.damage);
                                            int.TryParse(reader["def_damage"].ToString(), out unit.def_damage);


                                            int isTrue = 0;
                                            int.TryParse(reader["is_player1_unit"].ToString(), out isTrue);
                                            unit.isPlayer1Unit = isTrue > 0;

                                            isTrue = 0;
                                            int.TryParse(reader["is_defending"].ToString(), out isTrue);
                                            unit.isDefending = isTrue > 0;

                                            attackingUnits.Add(unit);
                                        }
                                    }

                                }
                            }

                            if(attackingUnits.Count > 0)
                            {
                                bool canStartBattle = true;

                                foreach (Data.Unit unit in attackingUnits)
                                {
                                    if (unit.current_x != unit.target_x || unit.current_y != unit.target_y)
                                    {
                                        canStartBattle = false;
                                        break;
                                    }
                                }

                                if (canStartBattle)
                                {
                                    Data.HexTile attackingArmyCamp = await GetArmyCampDataAsync(attackingUnits[0].gameID, attackingUnits[0].armyCamp_x, attackingUnits[0].armyCamp_y);
                                    List<Data.Unit> defendingUnits = new List<Data.Unit>();

                                    string selectDefendingUnits_query = String.Format("SELECT id, game_id, account_id, army_camp_x, army_camp_y, current_x, current_y, target_x, target_y, is_player1_unit, health, damage, def_damage, is_defending FROM units WHERE game_id = {0} AND army_camp_x = {1} AND army_camp_y = {2} AND is_defending = 1;", defendingArmyCamp.gameID, defendingArmyCamp.x, defendingArmyCamp.y);
                                    using (MySqlCommand selectDefendingUnits_command = new MySqlCommand(selectDefendingUnits_query, connection))
                                    {
                                        using (MySqlDataReader reader = selectDefendingUnits_command.ExecuteReader())
                                        {
                                            if (reader.HasRows)
                                            {
                                                while (reader.Read())
                                                {
                                                    Data.Unit unit = new Data.Unit();

                                                    long.TryParse(reader["id"].ToString(), out unit.databaseID);
                                                    int.TryParse(reader["game_id"].ToString(), out unit.gameID);
                                                    long.TryParse(reader["account_id"].ToString(), out unit.accountID);
                                                    int.TryParse(reader["army_camp_x"].ToString(), out unit.armyCamp_x);
                                                    int.TryParse(reader["army_camp_y"].ToString(), out unit.armyCamp_y);
                                                    int.TryParse(reader["current_x"].ToString(), out unit.current_x);
                                                    int.TryParse(reader["current_y"].ToString(), out unit.current_y);
                                                    int.TryParse(reader["target_x"].ToString(), out unit.target_x);
                                                    int.TryParse(reader["target_y"].ToString(), out unit.target_y);
                                                    int.TryParse(reader["health"].ToString(), out unit.health);
                                                    int.TryParse(reader["damage"].ToString(), out unit.damage);
                                                    int.TryParse(reader["def_damage"].ToString(), out unit.def_damage);


                                                    int isTrue = 0;
                                                    int.TryParse(reader["is_player1_unit"].ToString(), out isTrue);
                                                    unit.isPlayer1Unit = isTrue > 0;

                                                    isTrue = 0;
                                                    int.TryParse(reader["is_defending"].ToString(), out isTrue);
                                                    unit.isDefending = isTrue > 0;

                                                    defendingUnits.Add(unit);
                                                }
                                            }

                                        }
                                    }

                                    Data.Player attacker = await GetPlayerDataAsync(defendingArmyCamp.attackerAccountID);

                                    long attackerAccountID = defendingArmyCamp.attackerAccountID;

                                    Data.Player defender = await GetPlayerDataAsync(defendingArmyCamp.accountID);

                                    bool isAttackingCastle = false;

                                    if(defendingArmyCamp.x == defender.castle_x && defendingArmyCamp.y == defender.castle_y)
                                    {
                                        isAttackingCastle = true;
                                    }

                                    if (defendingUnits.Count > 0)
                                    {

                                        //Data.Player defender = await GetPlayerDataAsync(defendingArmyCamp.accountID);


                                        await Battle(attacker, defender, attackingArmyCamp, defendingArmyCamp, attackingUnits, defendingUnits, isAttackingCastle);
                                    }
                                    else
                                    {
                                        if (attacker.isPlayer1 == 1)
                                        {
                                            await UpdateHexTileTypeAsync(attacker.gameID, attackerAccountID, defendingArmyCamp.x, defendingArmyCamp.y, Terminal.HexType.PLAYER1_ARMY_CAMP);                                            
                                        }
                                        else
                                        {
                                            await UpdateHexTileTypeAsync(attacker.gameID, attackerAccountID, defendingArmyCamp.x, defendingArmyCamp.y, Terminal.HexType.PLAYER2_ARMY_CAMP);
                                        }

                                        await UpdateHexTileIsAttackingAsync(attacker.gameID, attackerAccountID, attackingArmyCamp.x, attackingArmyCamp.y, false);
                                        await UpdateHexTileIsDefendingAsync(attacker.gameID, attackerAccountID, defendingArmyCamp.x, defendingArmyCamp.y, false);
                                        await UpdateHexTileIsUnderAttackAsync(attacker.gameID, attackerAccountID, defendingArmyCamp.x, defendingArmyCamp.y, false);
                                        await UpdateArmyCampNeighboursAsync(defendingArmyCamp.gameID, attackerAccountID, attacker.isPlayer1, defendingArmyCamp);
                                        await UpdateBuildingsAndPlayerProduction(attackerAccountID);
                                        await UpdateBuildingsAndPlayerProduction(defendingArmyCamp.accountID);
                                        await UpdateUnitArmyCampAsync(attacker.gameID, attackerAccountID, defendingArmyCamp.x, defendingArmyCamp.y, attackingUnits);
                                        await UpdateArmyCampStatsAsync(attacker.gameID, attackerAccountID, defendingArmyCamp.x, defendingArmyCamp.y);

                                        if(isAttackingCastle == true)
                                        {
                                            await UpdateHasCastleAsync(defendingArmyCamp.accountID, 0, defendingArmyCamp.x, defendingArmyCamp.y);
                                        }
                                    }


                                }
                            }                            
                        }
                    }
                   
                    connection.Close();
                }

                return true;
            });
            return await task;

        }


        private async static Task<bool> Battle(Data.Player attacker, Data.Player defender, Data.HexTile attackingArmyCamp, Data.HexTile defendingArmyCamp, List<Data.Unit> attackingUnits, List<Data.Unit> defendingUnits, bool isAttackingCastle)
        {
            Task<bool> task = Task.Run(async () =>
            {                
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    long attackerAccountID = defendingArmyCamp.attackerAccountID;
                    long defenderAccountID = defendingArmyCamp.accountID;

                    await UpdateHexTileIsUnderAttackAsync(defendingArmyCamp.gameID, defendingArmyCamp.accountID, defendingArmyCamp.x, defendingArmyCamp.y, true);



                    if(defender.isPlayer1 == 1)
                    {
                        if(isAttackingCastle)
                        {
                            await UpdateHexTileTypeAsync(defendingArmyCamp.gameID, defendingArmyCamp.accountID, defendingArmyCamp.x, defendingArmyCamp.y, Terminal.HexType.PLAYER2_CASTLE_UNDER_ATTACK);
                        }
                        else
                        {
                            await UpdateHexTileTypeAsync(defendingArmyCamp.gameID, defendingArmyCamp.accountID, defendingArmyCamp.x, defendingArmyCamp.y, Terminal.HexType.PLAYER1_ARMY_CAMP_UNDER_ATTACK);
                        }
                    }
                    else
                    {
                        if (isAttackingCastle)
                        {
                            await UpdateHexTileTypeAsync(defendingArmyCamp.gameID, defendingArmyCamp.accountID, defendingArmyCamp.x, defendingArmyCamp.y, Terminal.HexType.PLAYER2_CASTLE_UNDER_ATTACK);
                        }
                        else
                        {
                            await UpdateHexTileTypeAsync(defendingArmyCamp.gameID, defendingArmyCamp.accountID, defendingArmyCamp.x, defendingArmyCamp.y, Terminal.HexType.PLAYER2_ARMY_CAMP_UNDER_ATTACK);
                        }
                    }                    

                    int result = await BattleResult(attackingUnits, defendingUnits);

                    if(result == 0) // attackers won
                    {                        
                        if(attacker.isPlayer1 == 1)
                        {
                            await UpdateHexTileTypeAsync(attacker.gameID, attackerAccountID, defendingArmyCamp.x, defendingArmyCamp.y, Terminal.HexType.PLAYER1_ARMY_CAMP);
                        }
                        else
                        {
                            await UpdateHexTileTypeAsync(attacker.gameID, attackerAccountID, defendingArmyCamp.x, defendingArmyCamp.y, Terminal.HexType.PLAYER2_ARMY_CAMP);
                        }


                        await UpdateHexTileIsAttackingAsync(attacker.gameID, attackerAccountID, attackingArmyCamp.x, attackingArmyCamp.y, false);
                        await UpdateHexTileIsDefendingAsync(attacker.gameID, attackerAccountID, defendingArmyCamp.x, defendingArmyCamp.y, false);
                        await UpdateHexTileIsUnderAttackAsync(attacker.gameID, attackerAccountID, defendingArmyCamp.x, defendingArmyCamp.y, false);
                        await UpdateArmyCampNeighboursAsync(attacker.gameID, attackerAccountID, attacker.isPlayer1, defendingArmyCamp);
                        await UpdateBuildingsAndPlayerProduction(attackerAccountID);
                        await UpdateBuildingsAndPlayerProduction(defenderAccountID);
                        await UpdateUnitArmyCampAsync(attacker.gameID, attackerAccountID, defendingArmyCamp.x, defendingArmyCamp.y, attackingUnits);
                        await UpdateArmyCampStatsAsync(attacker.gameID, attackerAccountID, defendingArmyCamp.x, defendingArmyCamp.y);

                        if (isAttackingCastle == true)
                        {
                            await UpdateHasCastleAsync(defendingArmyCamp.accountID, 0, defendingArmyCamp.x, defendingArmyCamp.y);
                        }
                    }
                    else // defenders won
                    {
                        if (defender.isPlayer1 == 1)
                        {
                            await UpdateHexTileTypeAsync(attacker.gameID, defenderAccountID, defendingArmyCamp.x, defendingArmyCamp.y, Terminal.HexType.PLAYER1_ARMY_CAMP);
                        }
                        else
                        {
                            await UpdateHexTileTypeAsync(attacker.gameID, defenderAccountID, defendingArmyCamp.x, defendingArmyCamp.y, Terminal.HexType.PLAYER2_ARMY_CAMP);
                        }
                        await UpdateHexTileIsAttackingAsync(attacker.gameID, attackerAccountID, attackingArmyCamp.x, attackingArmyCamp.y, false);
                        await UpdateHexTileIsDefendingAsync(attacker.gameID, defenderAccountID, defendingArmyCamp.x, defendingArmyCamp.y, false);
                        await UpdateHexTileIsUnderAttackAsync(attacker.gameID, defenderAccountID, defendingArmyCamp.x, defendingArmyCamp.y, false);
                        await UpdateArmyCampStatsAsync(defender.gameID, defenderAccountID, defendingArmyCamp.x, defendingArmyCamp.y);

                    }
                    connection.Close();
                }
                return true;
            });
            return await task;
        }

        private async static Task<int> BattleResult(List<Data.Unit> attackingUnits, List<Data.Unit> defendingUnits)
        {
            Task<int> task = Task.Run(async () =>
            {
                int result = 0;
                float gameTick = 1f / 5;
                int[] teamToAttackFirstDynamicWeights = new int[] { 50, 50 };
                int[] attackersDynamicDamageWeights = new int[] { 50, 25, 25 };
                int[] defendersDynamicDamageWeights = new int[] { 50, 25, 25 }; 


                while(attackingUnits.Count > 0 && defendingUnits.Count > 0)
                {
                    System.Threading.Thread.Sleep((int)(gameTick * 1000));

                    Data.Unit attacker = attackingUnits[new Random().Next(attackingUnits.Count)];
                    Data.Unit defender = defendingUnits[new Random().Next(defendingUnits.Count)];

                    int teamToAttackFirst = GetTeamToAttackFirst(teamToAttackFirstDynamicWeights);

                    if(teamToAttackFirst == 0)
                    {
                        int attackerDamageType = GetDamageType(attackersDynamicDamageWeights);
                        DealDamage(attacker.damage, attackerDamageType, defender);
                        if(defender.health == 0)
                        {
                            await DeleteUnitFromDB(defender.databaseID);
                            defendingUnits.Remove(defender);
                        }
                        else
                        {                          
                            int defenderDamageType = GetDamageType(defendersDynamicDamageWeights);
                            DealDamage(defender.def_damage, defenderDamageType, attacker);
                            if(attacker.health == 0)
                            {
                                await DeleteUnitFromDB(attacker.databaseID);
                                attackingUnits.Remove(attacker);
                            }
                        }

                    }
                    else
                    {
                        int defenderDamageType = GetDamageType(defendersDynamicDamageWeights);
                        DealDamage(defender.def_damage, defenderDamageType, attacker);
                        if (attacker.health == 0)
                        {
                            await DeleteUnitFromDB(attacker.databaseID);
                            attackingUnits.Remove(attacker);
                        }
                        else
                        {
                            int attackerDamageType = GetDamageType(attackersDynamicDamageWeights);
                            DealDamage(attacker.damage, attackerDamageType, defender);
                            if (defender.health == 0)
                            {
                                await DeleteUnitFromDB(defender.databaseID);
                                defendingUnits.Remove(defender);
                            }

                        }
                    }
                }

                result = (attackingUnits.Count == 0) ? 1 : 0;
                                           
                return result;
            });
            return await task;
        }
        
        private async static Task<bool> DeleteUnitFromDB(long databaseID)
        {
            Task<bool> task = Task.Run(() =>
            {                
                using (MySqlConnection connection = GetMySqlConnection())
                {
                    string delete_query = String.Format("DELETE FROM units WHERE id = {0};", databaseID);
                    using (MySqlCommand delete_command = new MySqlCommand(delete_query, connection))
                    {
                        delete_command.ExecuteNonQuery();                        
                    }
                    connection.Close();
                }
                return true;
            });
            return await task;
        }

        private static int GetTeamToAttackFirst(int[] teamToAttackFirstDynamicWeights)
        {
            Random randomAttackingTeam = new Random();
            int randomAttackingTeamIndex = randomAttackingTeam.Next(0, 100);
            int attackingFirstSum = 0;

            for (int i = 0; i < teamToAttackFirstDynamicWeights.Length; i++)
            {
                attackingFirstSum += teamToAttackFirstDynamicWeights[i];
                if (randomAttackingTeamIndex < attackingFirstSum)
                {

                    int decreaseAmount = 10;
                    int increaseAmount = 10;

                    teamToAttackFirstDynamicWeights[i] = Math.Max(1, teamToAttackFirstDynamicWeights[i] - decreaseAmount);

                    if (i == 0)
                    {
                        teamToAttackFirstDynamicWeights[1] += increaseAmount;
                    }
                    else
                    {
                        teamToAttackFirstDynamicWeights[0] += increaseAmount;
                    }

                    return i; // 0 = attackers, 1 = defenders                    
                }
            }
            return randomAttackingTeam.Next(0, 2);
        }

        private static int GetDamageType(int[] dynamicDamageWeights)
        {
            Random randomDamage = new Random();

            int randomDamageIndex = randomDamage.Next(0, 100);
            int damageTypeSum = 0;

            for (int i = 0; i < dynamicDamageWeights.Length; i++)
            {
                damageTypeSum += dynamicDamageWeights[i];
                if (randomDamageIndex < damageTypeSum)
                {
                    if (i != 0)
                    {
                        int decreaseAmount = 3;
                        int increaseAmount = 3;

                        dynamicDamageWeights[i] = Math.Max(1, dynamicDamageWeights[i] - decreaseAmount);

                        for (int j = 1; j < dynamicDamageWeights.Length; j++)
                        {
                            if (j != i)
                            {
                                dynamicDamageWeights[j] += increaseAmount;
                            }
                        }
                    }
                    return i; // 0 = normal damage, 1 = decreased damage, 2 = increased damage                  
                }
            }
            return randomDamage.Next(0, 3);
        }

        private static void DealDamage(int damageValue, int damageType, Data.Unit unitToBeDamaged)
        {
            switch (damageType)
            {
                case 0: // normal damage
                    unitToBeDamaged.health = (int)Math.Max(0, unitToBeDamaged.health - damageValue);
                    break;

                case 1: // decreased damage
                    unitToBeDamaged.health = (int)Math.Max(0, unitToBeDamaged.health - (damageValue * 0.5f));
                    break;

                case 2: // increased damage
                    unitToBeDamaged.health = (int)Math.Max(0, unitToBeDamaged.health - (damageValue * 1.5f));
                    break;
            }
        }



        public async static void LeaveMatch(int id, long accountID)
        {
            int response = await LeaveMatchAsync(id, accountID);

            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.LEAVE_MATCH);
            packet.Write(response);
            Sender.TCP_Send(id, packet);
        }

        private async static Task<int> LeaveMatchAsync(int id, long accountID)
        {            
            Task<int> task = Task.Run(() =>
            {
                int response = 0;

                using (MySqlConnection connection = GetMySqlConnection())
                {

                    string updateIsOnline_query = String.Format("UPDATE accounts SET in_game = 0, game_id = -1, stone = 0, wood = 0, food = 0, stone_production = 0, wood_production = 0, food_production = 0, has_castle = 0, castle_x = 0, castle_y = 0 WHERE id = {0};", accountID);
                    using (MySqlCommand updatePlayer1_command = new MySqlCommand(updateIsOnline_query, connection))
                    {
                        updatePlayer1_command.ExecuteNonQuery();
                        response = 1;
                    }
                    connection.Close();
                }

                return response;
            });
            return await task;
        }

        #endregion
    }
}