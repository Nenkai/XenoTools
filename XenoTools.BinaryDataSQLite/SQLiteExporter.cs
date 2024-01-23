using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using XenoTools.BinaryData;
using XenoTools.BinaryData.ID;

using Microsoft.Data.Sqlite;
using System.Diagnostics.Metrics;

namespace XenoTools.BinaryDataSQLite
{
    public class SQLiteExporter
    {
        private SqliteConnection _con;

        public void InitConnection(string sqliteDbFile)
        {
            _con = new SqliteConnection($"Data Source={sqliteDbFile}");
            _con.Open();
        }

        public void ExportTable(string tableName, List<BinaryDataIDMember> members, List<BinaryDataID> ids)
        {
            if (!char.IsLetter(tableName[0]))
                tableName = "_" + tableName;

            CreateSQLiteTable(tableName, members, ids);
            InsertIntoTable(tableName, members, ids);
        }

        private void InsertIntoTable(string tableName, List<BinaryDataIDMember> members, List<BinaryDataID> ids)
        {
            StringBuilder sb = new StringBuilder(1024);
            for (int i = 0; i < ids.Count; i++)
            {
                BinaryDataID id = ids[i];

                if (sb.Length == 0)
                {
                    sb.Append($"INSERT INTO {tableName} (id");
                    if (members.Count > 0)
                        sb.Append(", ");

                    for (int j = 0; j < members.Count; j++)
                    {
                        string memberName = GetValidColumnName(members, j, members[j].Name);
                        sb.Append(memberName);
                        if (j != members.Count - 1)
                            sb.Append(", ");
                    }
                    sb.Append(") VALUES");
                }

                sb.Append($" ({id.IDNumber}");
                if (id.Values.Count > 0)
                    sb.Append(", ");

                for (int j = 0; j < id.Values.Count; j++)
                {
                    BinaryDataValue val = id.Values[j];
                    if (val.Type == BinaryDataMemberType.Variable)
                    {
                        BinaryDataVariable variable = (BinaryDataVariable)val;

                        switch (variable.NumericType)
                        {
                            case BinaryDataVariableType.UByte:
                                sb.Append((byte)variable.Value);
                                break;

                            case BinaryDataVariableType.UShort:
                                sb.Append((ushort)variable.Value);
                                break;
                            case BinaryDataVariableType.UInt:
                                sb.Append((uint)variable.Value);
                                break;
                            case BinaryDataVariableType.SByte:
                                sb.Append((sbyte)variable.Value);
                                break;
                            case BinaryDataVariableType.Short:
                                sb.Append((short)variable.Value);
                                break;
                            case BinaryDataVariableType.Int:
                                sb.Append((int)variable.Value);
                                break;
                            case BinaryDataVariableType.String:
                                sb.Append($"'{(string)variable.Value}'");
                                break;
                            case BinaryDataVariableType.Float:
                                sb.Append((float)variable.Value);
                                break;
                            default:
                                throw new Exception("Invalid type");
                        }
                    }
                    else if (val.Type == BinaryDataMemberType.Array)
                    {
                        BinaryDataArray array = (BinaryDataArray)val;

                        string arrStr = "'";
                        for (int k = 0; k < array.Values.Count; k++)
                        {
                            object arrVal = array.Values[k];
                            switch (array.NumericType)
                            {
                                case BinaryDataVariableType.UByte:
                                    arrStr += (byte)arrVal;
                                    break;
                                case BinaryDataVariableType.UShort:
                                    arrStr += (ushort)arrVal;
                                    break;
                                case BinaryDataVariableType.UInt:
                                    arrStr += (uint)arrVal;
                                    break;
                                case BinaryDataVariableType.SByte:
                                    arrStr += (sbyte)arrVal;
                                    break;
                                case BinaryDataVariableType.Short:
                                    arrStr += (short)arrVal;
                                    break;
                                case BinaryDataVariableType.Int:
                                    arrStr += (int)arrVal;
                                    break;
                                case BinaryDataVariableType.String:
                                    arrStr += (string)arrVal;
                                    break;
                                case BinaryDataVariableType.Float:
                                    arrStr += (float)arrVal;
                                    break;
                                default:
                                    throw new Exception("Invalid type");
                            }

                            if (k < array.Values.Count - 1)
                                arrStr += ", ";
                        }
                        arrStr += "'";
                        sb.Append(arrStr);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }

                    if (j < id.Values.Count - 1)
                        sb.Append(", ");
                }

                if (i % 300 == 0 || i == ids.Count - 1)
                {
                    sb.Append(')');

                    SqliteCommand command = _con.CreateCommand();
                    command = _con.CreateCommand();
                    command.CommandText = sb.ToString();
                    command.ExecuteNonQuery();

                    sb.Clear();
                }
                else
                {
                    sb.Append("), ");
                }
            }
        }

        private void CreateSQLiteTable(string tableName, List<BinaryDataIDMember> members, List<BinaryDataID> ids)
        {
            Console.WriteLine($"Creating table {tableName}");

            SqliteCommand command = _con.CreateCommand();
            command.CommandText = $"DROP TABLE IF EXISTS {tableName};";
            command.ExecuteNonQuery();

            string str = $"CREATE TABLE {tableName} (id INTEGER";
            if (members.Count > 0)
                str += ", ";

            for (int i = 0; i < members.Count; i++)
            {
                string memberName = GetValidColumnName(members, i, members[i].Name);

                str += $"{memberName} {GetSQLIteType(members[i])}";
                if (i != members.Count - 1)
                    str += ", ";
            }
            str += ")";

            command = _con.CreateCommand();
            command.CommandText = str;
            command.ExecuteNonQuery();
        }

        private static string GetSQLIteType(BinaryDataIDMember member)
        {
            if (member.Type == BinaryDataMemberType.Variable)
            {
                switch (member.NumericType)
                {
                    case BinaryDataVariableType.UByte:
                    case BinaryDataVariableType.UShort:
                    case BinaryDataVariableType.UInt:
                    case BinaryDataVariableType.SByte:
                    case BinaryDataVariableType.Short:
                    case BinaryDataVariableType.Int:
                        return "INTEGER";
                    case BinaryDataVariableType.String:
                        return "TEXT";
                    case BinaryDataVariableType.Float:
                        return "REAL";
                    default:
                        throw new Exception("Invalid type");
                }
            }
            else if (member.Type == BinaryDataMemberType.Array)
            {
                return "TEXT";
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        private static string GetValidColumnName(List<BinaryDataIDMember> members, int colIndex, string name)
        {
            int sameNameCount = 0;
            for (int j = 0; j < colIndex; j++)
            {
                string memberName = members[j].Name;

                if (name.Equals(memberName, StringComparison.OrdinalIgnoreCase))
                    sameNameCount++;
            }

            if (sameNameCount == 0)
                return $"{GetFixedSqliteName(name)}";
            else
                return $"{GetFixedSqliteName(name)}_{sameNameCount + 1}";
        }

        private static string GetFixedSqliteName(string name)
        {
            switch (name.ToLower())
            {
                case "add":
                case "sub":
                case "default":
                case "index":
                case "group":
                case "select":
                case "limit":
                case "order":
                    return $"_{name}";
            }

            return name;
        }
    }
}
