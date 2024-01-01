using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

namespace XenoTools.BinaryData;

public class BinaryData
{
    public List<BinaryDataFile> Files { get; set; } = new();

    // Bdat::regist
    public void Regist(string path)
    {
        byte[] bdat = File.ReadAllBytes(path);
        SpanReader sr = new SpanReader(bdat);

        int numFiles = sr.ReadInt32();
        uint fileSize = sr.ReadUInt32();

        int[] fileOffsets = new int[numFiles];
        for (int i = 0; i < numFiles; i++)
            fileOffsets[i] = sr.ReadInt32();

        for (int i = 0; i < numFiles; i++)
        {
            var file = new BinaryDataFile();
            file.Read(bdat.AsMemory(fileOffsets[i]));
            Files.Add(file);
        }
    }

    public void Serialize(string fileName)
    {
        using var ms = new FileStream(fileName, FileMode.Create);
        using var bs = new BinaryStream(ms);

        bs.WriteInt32(Files.Count);
        bs.WriteInt32(0);

        bs.Position += Files.Count * sizeof(uint);

        long lastFilePos = bs.Position;
        for (int i = 0; i < Files.Count; i++)
        {
            bs.Position = 0x08 + i * 4;
            bs.WriteUInt32((uint)lastFilePos);

            bs.Position = lastFilePos;
            Files[i].Write(bs);

            lastFilePos = bs.Position;
        }

        bs.Position = 0x04;
        bs.WriteUInt32((uint)lastFilePos);
    }

    /// <summary>
    /// Gets the number of files in the bdat.
    /// </summary>
    /// <returns></returns>
    // Bdat::getFileCount - 0x71005AFEB0
    public int GetFileCount()
    {
        return Files.Count;
    }

    /// <summary>
    /// Gets the file name of a file in the bdat by index.
    /// </summary>
    /// <param name="fileIndex"></param>
    /// <returns></returns>
    // Bdat::getFileName - 0x71005AFF00
    public string GetFileName(int fileIndex)
    {
        return Files[fileIndex].Name;
    }

    /// <summary>
    /// Gets a file in the bdat by name.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    // Bdat::getFP - 0x71005AFF70
    public BinaryDataFile GetFilePointer(string name)
    {
        foreach (var file in Files)
        {
            if (file.Name == name)
                return file;
        }

        return null;
    }

    /// <summary>
    /// Gets a file in the bdat by index.
    /// </summary>
    /// <param name="fileIndex"></param>
    /// <returns></returns>
    // Bdat::getFP - 0x71005B0100
    public BinaryDataFile GetFP(int fileIndex)
    {
        return Files[fileIndex];
    }

    /// <summary>
    /// Returns whether a file exists in the bdat.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    // Bdat::isExistFP - 0x71005B0180
    public bool isExistFP(string name)
    {
        foreach (var file in Files)
        {
            if (file.Name == name)
                return true;
        }

        return false;
    }

}
