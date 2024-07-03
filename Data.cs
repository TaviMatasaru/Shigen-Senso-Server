using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace DevelopersHub.RealtimeNetworking.Server
{
    public static class Data
    {
        public class Player
        {
            public long accountID = -1;
            public string username = "username";

            public int victories = -1;
            public int rank = -1;

            public int gold = 100;
            public int gems = 10;
            public int wood = 1000;
            public int stone = 1000;
            public int food = 500;

            public int stoneProduction = 0;
            public int woodProduction = 0;
            public int foodProduction = 0;

            public bool hasCastle = false;
            public int castle_x = 0;
            public int castle_y = 0;

            public int isOnline = 0;
            public int isSearching = 0;
            public int inGame = 0;

            public long gameID = 0;
            public int isPlayer1 = 0;

            public List<Unit> units = new List<Unit>();
        }

        public class InitializationData
        {
            public long accountID = 0;
            public string username = "username";
            public List<ServerUnit> serverUnits = new List<ServerUnit>();
        }



        public class ServerBuilding
        {
            public string id = "";
            public long databaseID = 0;
            public int level;

            public int requiredGold = 0;
            public int requiredWood = 0;
            public int requiredStone = 0;

            public int stonePerSecond = 0;
            public int woodPerSecond = 0;
            public int foodPerSecond = 0;

            public int health = 0;
            public int max_capacity = 0;
        }

        public class ServerUnit
        {
            public UnitID id = UnitID.barbarian;
            public int level = 0;
            public int requiredFood = 0;
            public int housing = 1;
            public int health = 0;
            public int damage = 0;
            public int def_damage = 0;
            public int trainTime = 0;
        }



        public class HexTile
        {
            public long gameID = 0;
            public long accountID = -1;
            public long attackerAccountID = -1;
            public int hexType = 0;
            public int level = 1;
            public int x;
            public int y;

            public int requiredGold = 0;
            public int requiredStone = 0;
            public int requiredWood = 0;

            public int stonePerSecond = 0;
            public int woodPerSecond = 0;
            public int foodPerSecond = 0;

            public int health = 0;

            public int capacity = 0;

            public int attack = 0;
            public int defense = 0;

            public bool isAttacking = false;
            public bool isDefending = false;
            public bool isUnderAttack = false;
        }

        public class HexGrid
        {
            public int rows = 20;
            public int columns = 20;
            public List<HexTile> hexTiles = new List<HexTile>();
        }

        public class PathNode
        {
            public HexTile tile = new HexTile();
            public int gCost; // Cost from start node
            public int hCost; // Heuristic cost to end node
            public int FCost => gCost + hCost; // Total cost
            public PathNode cameFromNode; // To track the path

            public PathNode(HexTile tile)
            {
                this.tile = tile;
            }
            public PathNode()
            {

            }

            public bool IsWalkable()
            {
                switch ((Terminal.HexType)tile.hexType)
                {
                    case Terminal.HexType.FREE_MOUNTAIN:
                    case Terminal.HexType.FREE_FOREST:
                    //case Player.HexType.FREE_CROPS:
                    case Terminal.HexType.PLAYER1_MOUNTAIN:
                    case Terminal.HexType.PLAYER1_FOREST:
                    case Terminal.HexType.PLAYER2_MOUNTAIN:
                    case Terminal.HexType.PLAYER2_FOREST:
                        //case Player.HexType.PLAYER_CROPS:
                        return false;
                    default:
                        return true;
                }
            }
        }



        public enum UnitID
        {
            barbarian,
            archer
        }        
        public class Unit
        {
            public UnitID id = UnitID.barbarian;
            public int gameID = 0;
            public long accountID = -1;
            public int level = 0;
            public long databaseID = 0;
            public int housing = 1;
            public bool trained = false;
            public bool ready_player1 = false;
            public bool ready_player2 = false;
            public int health = 0;
            public int damage = 0;
            public int def_damage = 0;
            public int trainTime = 0;
            public float trainedTime = 0;
            public int armyCamp_x = 0;
            public int armyCamp_y = 0;
            public int current_x = 0;
            public int current_y = 0;
            public int target_x = 0;
            public int target_y = 0;
            public bool isPlayer1Unit = true;
            public bool isDefending = true;

            public string serializedPath;
            
        }
       


        public enum PlayerStatus
        {
            IN_GAME = 0,
            LEFT = 1,
            DISCONNECTED = 2,
            CASTLE_DESTROYED = 3
        }

        public enum GameResultID
        {
            NOT_OVER = 0,
            P1_WON = 1,
            P2_WON = 2,
            P1_LEFT = 3,
            P2_LEFT = 4
        }

        public class GameData
        {
            public long gameID = -1;
            public long player1AccountID = -1;
            public long player2AccountID = -1;
            public GameResultID gameResult = 0;         
            public PlayerStatus player1Status = 0;
            public PlayerStatus player2Status = 0;            
        }

        public class Game
        {
            public string player1_username = "";
            public string player2_username = "";
            public int player1_victories = 0;
            public int player2_victories = 0;
            public int player1_rank = 0;
            public int player2_rank = 0;

            public GameData gameData = new GameData();
        }



        public async static Task<string> Serialize<T>(this T target)
        {
            Task<string> task = Task.Run(() => {
                XmlSerializer xml = new XmlSerializer(typeof(T));
                StringWriter writer = new StringWriter();
                xml.Serialize(writer, target);
                return writer.ToString();
            });
            return await task;
        }

        public async static Task<T> Deserialize<T>(this string target)
        {

            Task<T> task = Task.Run(() => {
                XmlSerializer xml = new XmlSerializer(typeof(T));
                StringReader reader = new StringReader(target);
                return (T)xml.Deserialize(reader);
            });
            return await task;
            
        }

        public struct Vector2Int
        {
            public int x;
            public int y;

            public Vector2Int(int x, int y)
            {
                this.x = x;
                this.y = y;
            }

            public static Vector2Int operator +(Vector2Int a, Vector2Int b)
            {
                return new Vector2Int(a.x + b.x, a.y + b.y);
            }
        }

    }
}
