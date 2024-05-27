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
            
        }
        #endregion

        #region Data

        public enum RequestsID
        {
            AUTH = 1,
            SYNC = 2,
            NEW_GRID = 3,
            SYNC_GRID = 4,
            BUILD_CASTLE = 5,
            BUILD_STONE_MINE = 6,
            BUILD_SAWMILL = 7,
            BUILD_FARM = 8,
            BUILD_ARMY_CAMP = 9
            
        }

        public enum HexType
        {
            FREE_LAND = 0,
            FREE_MOUNTAIN = 1,
            FREE_FOREST = 2,
            FREE_CROPS = 3,
            PLAYER_LAND = 4,
            PLAYER_MOUNTAIN = 5,
            PLAYER_FOREST = 6,
            PLAYER_CROPS = 7,
            PLAYER_CASTLE = 8,
            PLAYER_STONE_MINE = 9,
            PLAYER_SAWMILL = 10,
            PLAYER_FARM = 11,
            PLAYER_ARMY_CAMP = 12
        }

        public static void ReceivedPacket(int clientID, Packet packet)
        {
            int id = packet.ReadInt();
            string deviceID = "";
            switch ((RequestsID)id)
            {
                case RequestsID.AUTH:
                    deviceID = packet.ReadString();
                    Database.AuthenticatePlayer(clientID, deviceID);
                    break;

                case RequestsID.SYNC:
                    deviceID = packet.ReadString();
                    Database.GetPlayerData(clientID, deviceID);
                    break;

                case RequestsID.NEW_GRID:
                    deviceID = packet.ReadString();                    
                    Database.GenerateNewGrid(clientID, deviceID);
                    break;

                case RequestsID.SYNC_GRID:
                    deviceID = packet.ReadString();
                    Database.SyncGrid(clientID, deviceID);
                    break;


                case RequestsID.BUILD_CASTLE:
                    deviceID = packet.ReadString();
                    int castle_x_pos = packet.ReadInt();
                    int castle_y_pos = packet.ReadInt();
                    Database.BuildCastle(clientID, deviceID, castle_x_pos, castle_y_pos);
                    break;

                case RequestsID.BUILD_STONE_MINE:
                    deviceID = packet.ReadString();
                    int stoneMine_x_pos = packet.ReadInt();
                    int stoneMine_y_pos = packet.ReadInt();
                    Database.BuildStoneMine(clientID, deviceID, stoneMine_x_pos, stoneMine_y_pos);
                    break;

                case RequestsID.BUILD_SAWMILL:
                    deviceID = packet.ReadString();
                    int sawmill_x_pos = packet.ReadInt();
                    int sawmill_y_pos = packet.ReadInt();
                    Database.BuildSawmill(clientID, deviceID, sawmill_x_pos, sawmill_y_pos);
                    break;

                case RequestsID.BUILD_FARM:
                    deviceID = packet.ReadString();
                    int farm_x_pos = packet.ReadInt();
                    int farm_y_pos = packet.ReadInt();
                    Database.BuildFarm(clientID, deviceID, farm_x_pos, farm_y_pos);
                    break;

                case RequestsID.BUILD_ARMY_CAMP:
                    deviceID = packet.ReadString();
                    int armyCamp_x_pos = packet.ReadInt();
                    int armyCamp_y_pos = packet.ReadInt();
                    Database.BuildArmyCamp(clientID, deviceID, armyCamp_x_pos, armyCamp_y_pos);
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