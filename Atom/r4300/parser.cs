﻿using mzxrules.Helper;
using mzxrules.OcaLib;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;

using static Atom.MipsFields;
namespace Atom
{
    public partial class Disassemble
    {
        internal static void Task(StreamWriter sw, BinaryReader br, DisassemblyTask task)
        {
            foreach(Section section in task.Sections.Values.OrderBy(x => x.VRam))
            {
                if (section.IsCode)
                {
                    TextDisassembly(sw, br, section);
                }
                else if (section.Name == "bss")
                {
                    Bss(sw, section);
                }
                else
                {
                    DataDisassembly(sw, br, section, task.Relocations.Where(x => x.RelocType == Reloc.R_MIPS32));
                }
            }
        }


        /// <summary>
        /// Performs a dissassembly using the initialized function list
        /// </summary>
        /// <param name="sw">the output disassembly</param>
        /// <param name="br">source file to disassemble</param>
        /// <param name="task">meta-data container for the disassembly task</param>
        internal static void TextDisassembly(StreamWriter sw, BinaryReader br, Section section)
        {
            WriteSectionName(sw, section);
            SimpleDisassembly(sw, br, section);
            sw.WriteLine();
        }

        internal static void SimpleDisassembly(StreamWriter sw, BinaryReader br, Section section)
        {
            //disable symbol detection
            First_Parse = false;
            jaltaken = 0;

            pc = section.VRam; 

            br.BaseStream.Position = section.Offset;
            Reset_Gpr_Regs();

            int textsize = section.Size;
            for (int i = 0; i < textsize; i += 4)
            {
                if (Symbols.ContainsKey(pc))
                {
                    var label = Symbols[pc];

                    //if function start
                    if (label.Kind == Label.Type.FUNC)
                    {
                        PrintFunctionStart(sw);
                        Reset_Gpr_Regs();
                    }
                    else
                    {
                        sw.WriteLine($"{label}:");
                    }
                }

                //read and write opcode
                int word = br.ReadBigInt32();
                sw.WriteLine($"\t{FormatOp(GetOP(word))}");

                if (jaltaken != 0)
                {
                    jaltaken--;
                    if (!(jaltaken != 0))
                        Reset_Gpr_Regs_Soft();
                }

                pc += 4;

                if (Symbols.ContainsKey(pc)
                    && Symbols[pc].Kind == Label.Type.FUNC)
                {
                    sw.WriteLine();
                    sw.WriteLine();
                }
            }

        }

        internal static void DataDisassembly(StreamWriter sw, BinaryReader br, Section section, IEnumerable<Overlay.RelocationWord> rel)
        {
            WriteSectionName(sw, section);
            sw.WriteLine();

            br.BaseStream.Position = section.Offset;
            pc = section.VRam;

            List<byte> byteChain = new List<byte>();
            N64Ptr end = section.VRam + section.Size;

            while (pc < end)
            {
                if (Symbols.ContainsKey(pc))
                {
                    DumpByteChain(sw, ref byteChain);
                    sw.Write($"{Symbols[pc]}: ");
                }

                if (rel.Any(x => x.Offset == br.BaseStream.Position))
                {
                    DumpByteChain(sw, ref byteChain);
                    N64Ptr lbl = br.ReadBigInt32();
                    pc += 4;
                    sw.WriteLine($".word {Symbols[lbl]}");
                }
                else
                {
                    byteChain.Add(br.ReadByte());
                    pc += 1;
                }
            }
            DumpByteChain(sw, ref byteChain);
            sw.WriteLine();
        }

        private static void WriteSectionName(StreamWriter w, Section section)
        {
            w.WriteLine($".section .{section.Name}");
            if (section.Subsection > 0)
                w.WriteLine($".subsection {section.Subsection}");
        }

        private static void Bss(StreamWriter sw, Section section)
        {
            N64Ptr bssStart = section.VRam;
            N64Ptr bssEnd = bssStart + section.Size;
            //.bss
            var bssLabels = Symbols.Where(x => x.Key >= bssStart && x.Key < bssEnd).Select(x => x.Value).OrderBy(x => x.Addr).ToList();
            if (bssLabels.Count == 0)
                return;

            bssLabels.Add(new Label(Label.Type.VAR, bssEnd, false));
            sw.WriteLine(".bss");
            sw.WriteLine();
            var firstBss = bssLabels[0];
            if (firstBss.Addr != bssStart)
            {
                sw.WriteLine($".space 0x{firstBss.Addr - bssStart:X2}");
            }
            for (int i = 0; i < bssLabels.Count - 1; i++)
            {
                var cur = bssLabels[i];
                var next = bssLabels[i + 1];
                sw.Write($"{cur}: ");
                sw.WriteLine($".space 0x{next.Addr - cur.Addr:X2}");
            }
        }

        private static void DumpByteChain(StreamWriter outputf, ref List<byte> bytes)
        {
            const int width = 16;

            if (bytes.Count == 0)
                return;

            bool printWords = bytes.Count % 4 == 0;
            string type = (printWords) ? "word" : "byte";

            if (bytes.Count > width)
            {
                outputf.WriteLine($".{type} \\");
            }
            else
            {
                outputf.Write($".{type} ");
            }

            for (int i = 0; i < bytes.Count; i += width)
            {
                var loop = (bytes.Count - i < width) ? bytes.Count - i : width;
                var line = (printWords) ? GetLineWord(bytes, i, loop) : GetLineByte(bytes, i, loop);
                outputf.Write(line);
                if (i + width < bytes.Count)
                    outputf.WriteLine(", \\");
            }
            outputf.WriteLine();
            bytes.Clear();
            
            string GetLineByte(List<byte> chain, int index, int count)
            {
                List<string> values = new List<string>();
                foreach (var item in chain.GetRange(index, count))
                {
                    values.Add($"0x{item:X2}");
                }
                return string.Join(", ", values);
            }
            string GetLineWord(List<byte> chain, int index, int count)
            {
                List<string> values = new List<string>();
                List<byte> items = chain.GetRange(index, count);
                for (int i = 0; i < count; i += 4)
                {
                    values.Add($"0x{items[i + 0]:X2}{items[i + 1]:X2}{items[i + 2]:X2}{items[i + 3]:X2}");
                }
                return string.Join(", ", values);
            }
        }
        
        public static string FormatOp(string op)
        {
            string comment = "";
            string opStr = op;
            var index = op.IndexOf("##");
            if (index > 0)
            {
                comment = op.Substring(index);
                opStr = op.Substring(0, index).Trim();
            }
            string[] ops = opStr.Split(new char[] { '\t' });
            if (ops.Length != 2)
            {
                return op;
            }

            return $"{ops[0],-7} {ops[1],-26} {comment}";
        }

        private static void PrintFunctionStart(StreamWriter sw)
        {
            Label curFunc = Symbols[pc];

            sw.WriteLine($"{curFunc}:");
            PrintCommentLines(curFunc.Name);
            PrintCommentLines(curFunc.Desc);
            PrintCommentLines(curFunc.Desc2);
            PrintCommentLines(curFunc.Args);
            //
            void PrintCommentLines(string v)
            {
                if (!string.IsNullOrWhiteSpace(v))
                    foreach (var line in v.Split(new char[] { ';' }))
                        sw.WriteLine($"## {line}");
            }
        }

        #region SymbolDetection

        internal static void FirstParse(BinaryReader br, DisassemblyTask task)
        {
            InitRelocationLabels(br, task);

            foreach (Section s in task.Sections.Values)
            {
                if (s.IsCode)
                    FirstParse(br, s);
            }
            //complete the label list
            RemoveFalseFunctions();

            List<(N64Ptr start, N64Ptr end)> textSections = GetTextSectionRanges(task);
            foreach (var addr in RelocationLabels.Values)
            {
                if (!Symbols.ContainsKey(addr))
                {
                    if (textSections.Exists(x => x.start >= addr && addr < x.end))
                    {
                        Symbols[addr] = new Label(Label.Type.LBL, addr);
                    }
                    else
                    {
                        Symbols[addr] = new Label(Label.Type.VAR, addr);
                    }
                }
            }
        }

        /// <summary>
        /// Performs a first pass over the assembly, computing various constants 
        /// </summary>
        /// <param name="br"></param>
        /// <param name="task"></param>
        internal static void FirstParse(BinaryReader br, Section section)
        {
            br.BaseStream.Position = section.Offset;
            pc = section.VRam;

            int word;
            int opId;

            int text_size = section.Size;

            First_Parse = true;

            Reset_Gpr_Regs();

            //Start of text will always be some function
            AddFunction(pc, true);

            // First scan - just to map out branches/jumps 
            for (int i = 0; i < text_size; i += 4)
            {
                word = br.ReadBigInt32();
                GetOP(word);
                opId = (word >> 26) & 0x3F;
                if (opId == 2 || opId == 3)
                {
                    var addr = TARGET((uint)word) | 0x80000000;
                    AddFunction(addr, true);
                }

                if (pc == (EndOfFunction + 4))
                {
                    if (word != 0)
                    {
                        AddFunction(pc, false);
                        Reset_Gpr_Regs();
                    }
                    else
                        EndOfFunction += 4;
                }
                pc += 4;
            }
            First_Parse = false;
            Reset_Gpr_Regs();
        }

        private static void RemoveFalseFunctions()
        {
            foreach (var func in Symbols.Values.Where(x => x.Kind == Label.Type.FUNC && x.Confirmed == false))
            {
                if (Symbols.TryGetValue(func.Addr + 4, out Label lbl))
                {
                    func.Kind = Label.Type.LBL;
                }
            }
        }

        private static void InitRelocationLabels(BinaryReader br, DisassemblyTask task)
        {
            N64Ptr hi_addr = 0;
            Rel_Parse = true;

            N64Ptr start = task.VRam.Start;
            List<(N64Ptr start, N64Ptr end)> textSections = GetTextSectionRanges(task);

            foreach (var reloc in task.Relocations)
            {
                N64Ptr relAddr = task.VRam.Start + reloc.Offset;
                br.BaseStream.Position = reloc.Offset;

                if (reloc.RelocType == Reloc.R_MIPS_HI16
                    || reloc.RelocType == Reloc.R_MIPS_LO16)
                {
                    GetOP(br.ReadBigInt32());
                    if (reloc.RelocType == Reloc.R_MIPS_HI16)
                    {
                        hi_addr = relAddr;
                    }
                    else if (reloc.RelocType == Reloc.R_MIPS_LO16)
                    {
                        //Fixes bug where gcc generates a R_MIPS_HI16 
                        //after LO16 that is part of the same chain of LO16s
                        if (!RelocationLabels.ContainsKey(hi_addr))
                        {
                            RelocationLabels[hi_addr] = Rel_Label_Addr;
                        }
                        RelocationLabels[relAddr] = Rel_Label_Addr;
                    }
                }

                else if (reloc.RelocType == Reloc.R_MIPS32)
                {
                    N64Ptr ptr = br.ReadBigInt32();

                    if (textSections.Exists(x => x.start >= ptr && ptr < x.end))
                    {
                        AddLabel(ptr, false);
                    }
                    else
                        Symbols[ptr] = new Label(Label.Type.VAR, ptr);
                }
            }

            Rel_Parse = false;
            br.BaseStream.Position = 0;
        }

        private static List<(N64Ptr start, N64Ptr end)> GetTextSectionRanges(DisassemblyTask task)
        {
            List<(N64Ptr start, N64Ptr end)> textSections = new List<(N64Ptr start, N64Ptr end)>();
            foreach (var section in task.Sections.Values)
            {
                if (section.IsCode)
                {
                    textSections.Add((section.VRam, section.Size));
                }
            }

            return textSections;
        }

        internal static List<Label> GetFunctions()
        {
            return Symbols.Values.Where(x => x.Kind == Label.Type.FUNC).ToList();
        }

        
        internal static void AddFunctions(IEnumerable<FunctionInfo> funcs)
        {
            foreach(var f in funcs)
            {
                AddFunction(new Label(f));
            }
        }

        internal static void AddFunction(Label f)
        {
            if (!Symbols.ContainsKey(f.Addr))
                Symbols.Add(f.Addr, f);
        }
        
        /// <summary>
        /// Should return bool
        /// </summary>
        /// <param name="addr"></param>
        /// <param name="confirmed"></param>
        /// <returns></returns>
        static void AddFunction(N64Ptr addr, bool confirmed)
        {
            addr = addr | 0x80000000;

            // Label already mapped?
            if (Symbols.TryGetValue(addr, out Label lbl))
            {
                if (lbl.Kind != Label.Type.FUNC)
                {
                    if (confirmed || !lbl.Confirmed)
                    {
                        lbl.Kind = Label.Type.FUNC;
                    }
                }
                if (lbl.Kind == Label.Type.FUNC)
                {
                    lbl.hits++;
                    if (confirmed)
                        lbl.Confirmed = confirmed;
                }
            }
            else
            {
                Symbols.Add(addr, new Label(Label.Type.FUNC, addr, confirmed));
            }
        }

        static Label AddLabel(N64Ptr addr, bool confirmed = true)
        {
            if (!Symbols.TryGetValue(addr, out Label label)
                || (label.Confirmed == false && confirmed == true))
            {
                label = new Label(Label.Type.LBL, addr, confirmed);
                Symbols[addr] = label;
            }

            return label;
        }

        #endregion

    }
}