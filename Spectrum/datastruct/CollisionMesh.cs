﻿using System;
using System.Collections.Generic;
using mzxrules.Helper;

namespace Spectrum
{
    class CollisionMesh
    {
        /* 0x00 */ Vector3<short> BoundsMin = new Vector3<short>();
        /* 0x06 */ Vector3<short> BoundsMax = new Vector3<short>();
        /* 0x0C */ public int Vertices;
        /* 0x10 */ public Ptr VertexArray;
        /* 0x14 */ public int Polys;
        /* 0x18 */ public Ptr PolyArray;
        /* 0x1C */ public Ptr PolyTypeArray;
        /* 0x20 */ public Ptr CameraDataArray;
        /* 0x24 */ public short WaterBoxes;
        /* 0x28 */ public Ptr WaterBoxesArray;

        public CollisionMesh(Ptr ptr)
        {
            BoundsMin = new Vector3<short>(
                ptr.ReadInt16(0x00),
                ptr.ReadInt16(0x02),
                ptr.ReadInt16(0x04));
            BoundsMax = new Vector3<short>(
                ptr.ReadInt16(0x06),
                ptr.ReadInt16(0x08),
                ptr.ReadInt16(0x0A));
            Vertices = ptr.ReadInt16(0x0C);
            VertexArray = ptr.Deref(0x10);
            Polys = ptr.ReadInt16(0x14);
            PolyArray = ptr.Deref(0x18);
            PolyTypeArray = ptr.Deref(0x1C);
            CameraDataArray = ptr.Deref(0x20);
            WaterBoxes = ptr.ReadInt16(0x24);
            WaterBoxesArray = ptr.Deref(0x28);
        }
        public override string ToString()
        {
            var bMin = $"{BoundsMin.x}, {BoundsMin.y}, {BoundsMin.z}";
            var bMax = $"{BoundsMax.x}, {BoundsMax.y}, {BoundsMax.z}";
            return
                $"Bound: ({bMin}) ({bMax}){Environment.NewLine}" +
                $"Verts: {Vertices,4}  {VertexArray}{Environment.NewLine}" +
                $"Polys: {Polys,4}  {PolyArray}{Environment.NewLine}" +
                $"Types:       {PolyTypeArray}{Environment.NewLine}" +
                $"Cams :       {CameraDataArray}{Environment.NewLine}" +
                $"Water: {WaterBoxes,4}  {WaterBoxesArray}";
        }

        public string GetPolyFormattedInfo(N64Ptr ptr)
        {
            CollisionPoly poly = new CollisionPoly(this, SPtr.New(ptr));
            return poly.ToString();
        }

        public CollisionPoly GetPolyById(int id)
        {
            return new CollisionPoly(this, PolyArray.RelOff(id * 0x10));
        }
    }

    class CollisionPoly
    {
        int Id;
        CollisionPolyType Type;
        CollisionVertex VertexA, VertexB, VertexC;
        byte VertexFlagsA;
        byte VertexFlagsB;
        byte VertexFlagsC;
        Vector3<short> Normal;
        short D;
        public CollisionPoly(CollisionMesh mesh, Ptr ptr)
        {
            short type;
            ushort vA;
            ushort vB;
            ushort vC;
            short vertexA;
            short vertexB;
            short vertexC;

            Id = (ptr - mesh.PolyArray) / 0x10;

            type = ptr.ReadInt16(0x00);
            vA = ptr.ReadUInt16(0x02);
            vB = ptr.ReadUInt16(0x04);
            vC = ptr.ReadUInt16(0x06);
            vertexA = (short)(vA & 0x1FFF);
            vertexB = (short)(vB & 0x1FFF);
            vertexC = (short)(vC & 0x1FFF);
            VertexFlagsA = Shift.AsByte(vA, 0xE000);
            VertexFlagsB = Shift.AsByte(vB, 0xE000);
            VertexFlagsC = Shift.AsByte(vC, 0xE000);

            Normal = new Vector3<short>(
                ptr.ReadInt16(0x08),
                ptr.ReadInt16(0x0A),
                ptr.ReadInt16(0x0C));
            D = ptr.ReadInt16(0x0E);

            Type = CollisionPolyType.GetPolyType(mesh, type);
            VertexA = CollisionVertex.GetVertex(mesh, vertexA);
            VertexB = CollisionVertex.GetVertex(mesh, vertexB);
            VertexC = CollisionVertex.GetVertex(mesh, vertexC);
        }
        public string TSV()
        {
            return $"{Type.Id:X4}\t{VertexFlagsA:X1}\t" +
                $"{VertexA.Id:X4}\t{VertexA.Value.x}\t{VertexA.Value.y}\t{VertexA.Value.z}\t" +
                $"{VertexB.Id:X4}\t{VertexB.Value.x}\t{VertexB.Value.y}\t{VertexB.Value.z}\t" +
                $"{VertexC.Id:X4}\t{VertexC.Value.x}\t{VertexC.Value.y}\t{VertexC.Value.z}\t" +
                $"{Normal.x}\t{Normal.y}\t{Normal.z}\t{D}";
        }
        public override string ToString()
        {
            Vector3<float> unit = new Vector3<float>(
                (float)Normal.x / 32767,
                (float)Normal.y / 32767,
                (float)Normal.z / 32767
            );
            return $"Id: {Id:X4} Type: {Type}{Environment.NewLine}" +
                $"VertA: {VertexFlagsA:X1} {VertexA}{Environment.NewLine}" +
                $"VertB: {VertexFlagsB:X1} {VertexB}{Environment.NewLine}" +
                $"VertC: {VertexFlagsC:X1} {VertexC}{Environment.NewLine}" +
                $"Normal: ({Normal.x:X4},{Normal.y:X4},{Normal.z:X4}) : ({unit.x:F3},{unit.y:F3},{unit.z:F3}) + {D}";
        }
    }

}
