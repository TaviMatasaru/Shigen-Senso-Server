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

        public async static void AuthenticatePlayer(int id, string device)
        {
            long account_id = await AuthenticatePlayerAsync(id, device);
            Server.clients[id].device = device;
            Server.clients[id].account = account_id;
            Sender.TCP_Send(id, 1, account_id);
        }

        private async static Task<long> AuthenticatePlayerAsync(int id, string device)
        {
            Task<long> task = Task.Run(() =>
            {
                long account_id = 0;
                string select_query = String.Format("SELECT id FROM accounts WHERE device_id = '{0}';", device);
                bool userFound = false;
                using (MySqlCommand select_command = new MySqlCommand(select_query, mysqlConnection))
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
                    using (MySqlCommand insert_command = new MySqlCommand(insert_query, mysqlConnection))
                    {
                        insert_command.ExecuteNonQuery();
                        account_id = insert_command.LastInsertedId;
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
            List<Data.Building> buildings = await GetBuildingsAsync(accountID);
            data_player.buildings = buildings;
            Packet packet = new Packet();
            packet.Write(2);
            packet.Write(Data.Serialize<Data.Player>(data_player));
     
            Sender.TCP_Send(id, packet);
        }

        private async static Task<Data.Player> GetPlayerDataAsync(int id, string device)
        {
            Task<Data.Player> task = Task.Run(() =>
            {
                Data.Player data = new Data.Player();
                string select_query = String.Format("SELECT id, gold, gems, wood, stone, food FROM accounts WHERE device_id = '{0}';", device);            
                using (MySqlCommand select_command = new MySqlCommand(select_query, mysqlConnection))
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
                                data.wood = int.Parse(reader["wood"].ToString());
                                data.stone = int.Parse(reader["stone"].ToString());
                                data.food = int.Parse(reader["food"].ToString());

                            }
                        }
                    }
                }               
                return data;
            });
            return await task;
        }

        public async static void PlaceBuilding(int id, string device, string buildingID, int x_pos, int y_pos)
        {
            Data.Player player = await GetPlayerDataAsync(id, device);
            Data.ServerBuilding building = await GetServerBuildingAsync(buildingID, 1);
            if(player.gold >= building.requiredGold && player.wood >= building.requiredWood && player.stone >= building.requiredStone)
            {
                long accountID = Server.clients[id].account;
                List<Data.Building> buildings = await GetBuildingsAsync(accountID);
                bool canPlaceBuilding = true;

            }
        }

        private async static Task<Data.Building> GetBuildingAsync(long account_id, string buildingID)
        {
            Task<Data.Building> task = Task.Run(() =>
            {
                Data.Building data = new Data.Building();
                data.id = buildingID;
                string select_query = String.Format("SELECT id, level, x_position, y_position FROM buildings WHERE account_id = {0} AND global_id = '{1}';", account_id,  buildingID);
                using (MySqlCommand select_command = new MySqlCommand(select_query, mysqlConnection))
                {
                    using (MySqlDataReader reader = select_command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                data.databaseID = long.Parse(reader["id"].ToString());
                                data.level = int.Parse(reader["level"].ToString());
                                data.x = int.Parse(reader["x_position"].ToString());
                                data.y = int.Parse(reader["y_position"].ToString());
                            }
                        }
                    }
                }
                return data;
            });
            return await task;
        }

        private async static Task<List<Data.Building>> GetBuildingsAsync(long account_id)
        {
            Task<List<Data.Building>> task = Task.Run(() =>
            {
                List<Data.Building> data = new List<Data.Building>();
                string select_query = String.Format("SELECT id, global_id, level, x_position, y_position FROM buildings WHERE account_id = '{0}';", account_id);
                using (MySqlCommand select_command = new MySqlCommand(select_query, mysqlConnection))
                {
                    using (MySqlDataReader reader = select_command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                Data.Building building = new Data.Building();
                                building.id = reader["global_id"].ToString();
                                building.databaseID = long.Parse(reader["id"].ToString());
                                building.level = int.Parse(reader["level"].ToString());
                                building.x = int.Parse(reader["x_position"].ToString());
                                building.y = int.Parse(reader["y_position"].ToString());
                                data.Add(building);
                            }
                        }
                    }
                }
                return data;
            });
            return await task;
        }

        private async static Task<Data.ServerBuilding> GetServerBuildingAsync(string buildingID, int level)
        {
            Task<Data.ServerBuilding> task = Task.Run(() =>
            {
                Data.ServerBuilding data = new Data.ServerBuilding();
                data.id = buildingID;
                string select_query = String.Format("SELECT id,, required_gold, required_wood, required_stone FROM server_buildings WHERE global_id = '{0}' AND level = {1};", buildingID, level);
                using (MySqlCommand select_command = new MySqlCommand(select_query, mysqlConnection))
                {
                    using (MySqlDataReader reader = select_command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                data.databaseID = long.Parse(reader["id"].ToString());
                                data.level = level;
                                data.requiredGold = int.Parse(reader["required_gold"].ToString());
                                data.requiredWood = int.Parse(reader["required_wood"].ToString());
                                data.requiredStone = int.Parse(reader["required_stone"].ToString());
                            }
                        }
                    }
                }
                return data;
            });
            return await task;
        }

        #endregion
    }
}