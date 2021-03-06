﻿using mzxrules.Helper;
using mzxrules.OcaLib;
using System;
using System.IO;

namespace Gen
{
    public class FileSpitter
    {
        //public delegate void GenerateFile();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sourceRom">The source rom file</param>
        /// <param name="modifiedRom">The modified rom file</param>
        /// <param name="folder"></param>
        public static void GenerateModifiedFiles(ORom sourceRom, ORom modifiedRom, string folder)
        {
            BinaryReader source;
            BinaryReader modified;
            //FileRecord modifiedFileRecord;
            RomFile modifiedFile;


            foreach (FileRecord record in sourceRom.Files)
            {
                source = new BinaryReader(sourceRom.Files.GetFile(record.VirtualAddress));
                modifiedFile = modifiedRom.Files.GetFile(record.VirtualAddress);
                modified = new BinaryReader(modifiedFile);

                if (source.BaseStream.IsDifferentTo(modified.BaseStream))
                {
                    modified.BaseStream.Position = 0;
                    WriteFile(modified, modifiedFile.Record, record.IsCompressed, folder);
                }
            }
        }

        private static void WriteFile(BinaryReader file, FileRecord record, bool compress, string folder)
        {
            byte[] data;
            using (FileStream dest = new FileStream($"{record.VirtualAddress.Start}/{folder:X8}", FileMode.CreateNew))
            {
                data = new byte[record.VirtualAddress.Size];
                file.Read(data, 0, record.VirtualAddress.Size);

                if (compress)
                    Yaz.Encode(data, record.VirtualAddress.Size, dest);
                else
                    dest.Write(data, 0, data.Length);
            }
        }
        
    }

}
