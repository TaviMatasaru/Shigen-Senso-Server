using System;
using System.Numerics;

namespace DevelopersHub.RealtimeNetworking.Server
{
    class Terminal
    {

        #region Update
        public const int updatesPerSecond = 30;
        public static void Update()
        {
            Database.Update();
        }
        #endregion

        #region Connection
        public const int maxPlayers = 100000;
        public const int port = 5555;
        public static void OnClientConnected(int id, string ip)
        {
            
        }

        public static void OnClientDisconnected(int id, string ip)
        {
            Database.LogoutDisconnectedClient(id);
        }
        #endregion

        #region Data

        public enum RequestsID
        {
            LOGIN = 1,
            SYNC = 2,
            NEW_GRID = 3,
            SYNC_GRID = 4,
            BUILD_CASTLE = 5,
            BUILD_STONE_MINE = 6,
            BUILD_SAWMILL = 7,
            BUILD_FARM = 8,
            BUILD_ARMY_CAMP = 9,
            TRAIN = 10,
            CANCEL_TRAIN = 11,
            SEARCH = 12,
            CANCEL_SEARCH = 13,            
            UNIT_READY = 14,
            LAUNCH_ATTACK = 15,
            REGISTER = 16,
            AUTO_LOGIN = 17,
            LOGOUT = 18,
            LEAVE_MATCH = 19
        }

        public enum HexType
        {
            FREE_LAND = 0,
            FREE_MOUNTAIN = 1,
            FREE_FOREST = 2,
            FREE_CROPS = 3,
            PLAYER1_LAND = 4,
            PLAYER1_MOUNTAIN = 5,
            PLAYER1_FOREST = 6,
            PLAYER1_CROPS = 7,
            PLAYER1_CASTLE = 8,
            PLAYER1_STONE_MINE = 9,
            PLAYER1_SAWMILL = 10,
            PLAYER1_FARM = 11,
            PLAYER1_ARMY_CAMP = 12,
            PLAYER2_LAND = 13,
            PLAYER2_MOUNTAIN = 14,
            PLAYER2_FOREST = 15,
            PLAYER2_CROPS = 16,
            PLAYER2_CASTLE = 17,
            PLAYER2_STONE_MINE = 18,
            PLAYER2_SAWMILL = 19,
            PLAYER2_FARM = 20,
            PLAYER2_ARMY_CAMP = 21,
            PLAYER1_ARMY_CAMP_UNDER_ATTACK = 22,
            PLAYER2_ARMY_CAMP_UNDER_ATTACK = 23,
            PLAYER1_CASTLE_UNDER_ATTACK = 24,
            PLAYER2_CASTLE_UNDER_ATTACK = 25
        }

        public static void ReceivedPacket(int clientID, Packet packet)
        {
            int id = packet.ReadInt();
            string deviceID = "";
            switch ((RequestsID)id)
            {
                case RequestsID.LOGIN:
                    deviceID = packet.ReadString();
                    string loginUsername = packet.ReadString();
                    string loginPassword = packet.ReadString();
                    Database.LoginPlayer(clientID, deviceID, loginUsername, loginPassword);
                    break;

                case RequestsID.REGISTER:
                    deviceID = packet.ReadString();
                    string registerUsername = packet.ReadString();
                    string registerPassword = packet.ReadString();
                    Database.RegisterPlayer(clientID, deviceID, registerUsername, registerPassword);
                    break;

                case RequestsID.AUTO_LOGIN:
                    deviceID = packet.ReadString();
                    Database.AutoLoginPlayer(clientID, deviceID);
                    break;

                case RequestsID.LOGOUT:                 
                    string logoutUsername = packet.ReadString();
                    Database.LogoutPlayer(clientID, logoutUsername);
                    break;

                case RequestsID.SYNC:                    
                    Database.GetPlayerData(clientID);
                    break;               

                case RequestsID.SYNC_GRID:
                    Database.SyncGrid(clientID);
                    break;


                case RequestsID.BUILD_CASTLE:
                    int castle_x_pos = packet.ReadInt();
                    int castle_y_pos = packet.ReadInt();
                    Database.BuildCastle(clientID, castle_x_pos, castle_y_pos);
                    break;

                case RequestsID.BUILD_STONE_MINE:
                    int stoneMine_x_pos = packet.ReadInt();
                    int stoneMine_y_pos = packet.ReadInt();
                    Database.BuildStoneMine(clientID, stoneMine_x_pos, stoneMine_y_pos);
                    break;

                case RequestsID.BUILD_SAWMILL:
                    int sawmill_x_pos = packet.ReadInt();
                    int sawmill_y_pos = packet.ReadInt();
                    Database.BuildSawmill(clientID, sawmill_x_pos, sawmill_y_pos);
                    break;

                case RequestsID.BUILD_FARM:
                    int farm_x_pos = packet.ReadInt();
                    int farm_y_pos = packet.ReadInt();
                    Database.BuildFarm(clientID, farm_x_pos, farm_y_pos);
                    break;

                case RequestsID.BUILD_ARMY_CAMP:
                    int armyCamp_x_pos = packet.ReadInt();
                    int armyCamp_y_pos = packet.ReadInt();
                    Database.BuildArmyCamp(clientID, armyCamp_x_pos, armyCamp_y_pos);
                    break;

                case RequestsID.TRAIN:     
                    string trainUnitGlobalID = packet.ReadString();
                    int train_armyCamp_x = packet.ReadInt();
                    int train_armyCamp_y = packet.ReadInt();                  
                    Database.TrainUnit(clientID, trainUnitGlobalID, 1, train_armyCamp_x, train_armyCamp_y);
                    break;

                case RequestsID.CANCEL_TRAIN:
                    string cancelTrainUnitGlobalID = packet.ReadString();
                    int cancelTrain_armyCamp_x = packet.ReadInt();
                    int cancelTrain_armyCamp_y = packet.ReadInt();
                    Database.CancelTrainUnit(clientID, cancelTrainUnitGlobalID, cancelTrain_armyCamp_x, cancelTrain_armyCamp_y);
                    break;

                case RequestsID.SEARCH:
                    Database.StartSearching(clientID);
                    break;

                case RequestsID.CANCEL_SEARCH:
                    Database.CancelSearching(clientID);
                    break;

                case RequestsID.UNIT_READY:
                    long readyUnitDatabaseID = packet.ReadLong();
                    int grid_x = packet.ReadInt();
                    int grid_y = packet.ReadInt();
                    int isPlayer1 = packet.ReadInt();
                    Database.UpdateUnitReady(clientID, readyUnitDatabaseID, grid_x, grid_y, isPlayer1);
                    break;

                case RequestsID.LAUNCH_ATTACK:
                    int attackingUnitsCount = packet.ReadInt();
                    int attackingArmyCamp_x = packet.ReadInt();
                    int attackingArmyCamp_y = packet.ReadInt();
                    int defendingArmyCamp_x = packet.ReadInt();
                    int defenndingArmyCamp_y = packet.ReadInt();
                    Database.LaunchAttack(clientID, attackingUnitsCount, attackingArmyCamp_x, attackingArmyCamp_y, defendingArmyCamp_x, defenndingArmyCamp_y);
                    break;

                case RequestsID.LEAVE_MATCH:
                    long leavingMatchPlayerAccountID = packet.ReadLong();
                    Database.LeaveMatch(clientID, leavingMatchPlayerAccountID);
                    Console.WriteLine("Am primit LEAVE MATCH");

                    break;
            }      
        }

        public static void ReceivedBytes(int clientID, int packetID, byte[] data)
        {

        }

        public static void ReceivedString(int clientID, int packetID, string data)
        {
            
        }

        public static void ReceivedInteger(int clientID, int packetID, int data)
        {

        }

        public static void ReceivedFloat(int clientID, int packetID, float data)
        {

        }

        public static void ReceivedBoolean(int clientID, int packetID, bool data)
        {

        }

        public static void ReceivedVector3(int clientID, int packetID, Vector3 data)
        {

        }

        public static void ReceivedQuaternion(int clientID, int packetID, Quaternion data)
        {

        }

        public static void ReceivedLong(int clientID, int packetID, long data)
        {

        }

        public static void ReceivedShort(int clientID, int packetID, short data)
        {

        }

        public static void ReceivedByte(int clientID, int packetID, byte data)
        {

        }

        public static void ReceivedEvent(int clientID, int packetID)
        {

        }


        #endregion

    }
}