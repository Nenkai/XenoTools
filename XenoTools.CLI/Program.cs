using XenoTools.BinaryData;

namespace XenoTools.CLI
{
    internal class Program
    {
        static void Main(string[] args)
        {
            /*
            foreach (var bdatFile in Directory.GetFiles(@"romfs\bdat", "*.bdat", SearchOption.AllDirectories))
            {
                var bdat = new BinaryData();
                bdat.Regist(bdatFile);

                int numFiles = bdat.GetFileCount();
                for (int fileIndex = 0; fileIndex < numFiles; fileIndex++)
                {
                    string fileName = bdat.GetFileName(fileIndex);
                    Console.WriteLine(fileName);

                    BinaryDataFile file = bdat.GetFilePointer(fileName);

                    int idTop = file.GetIdTop();
                    int idCount = file.GetIdCount();
                    for (int idIdx = 0; idIdx < idCount; idIdx++)
                    {
                        int numMembers = file.GetMemberSize();
                        for (int memberIndex = 0; memberIndex < numMembers; memberIndex++)
                        {
                            BinaryDataMember member = file.GetMember(memberIndex);
                            //member = file.GetMember(member.Name);

                            //Console.WriteLine($"- {member}");

                            if (member.TypeDef.Type == BinaryDataMemberType.Variable)
                            {
                                object val = file.GetVar(member, idIdx);
                            }
                            else if (member.TypeDef.Type == BinaryDataMemberType.Array)
                            {
                                for (int k = 0; k < member.TypeDef.ArrayLength; k++)
                                {
                                    object val = file.GetArray(member, idIdx, k);
                                }
                            }
                            else if (member.TypeDef.Type == BinaryDataMemberType.Flag)
                            {
                                throw new NotImplementedException();
                            }
                        }
                    }
                }

                bdat.Serialize(Path.GetFileName(bdatFile));
            }
            */
        }
    }
}