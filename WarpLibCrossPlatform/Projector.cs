﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Accord;
using Warp.Headers;
using Warp.Tools;

namespace Warp
{
    public class Projector : IDisposable
    {
        private static readonly object CPUSync = new object();

        public int3 Dims, DimsOversampled;
        public int Oversampling;

        public Image Data;
        public Image Weights;

        public ulong t_DataRe, t_DataIm;
        ulong a_DataRe, a_DataIm;

        public Projector(int3 dims, int oversampling)
        {
            Dims = dims;
            Oversampling = oversampling;

            int Oversampled = 2 * (oversampling * (Dims.X / 2) + 1) + 1;
            DimsOversampled = new int3(Oversampled, Oversampled, Oversampled);

            Data = new Image(DimsOversampled, true, true);
            Weights = new Image(DimsOversampled, true, false);
        }

        public Projector(Image data, int oversampling, int projdim = 2)
        {
            Dims = data.Dims;
            Oversampling = oversampling;

            int Oversampled = 2 * (oversampling * (Dims.X / 2) + 1) + 1;
            DimsOversampled = new int3(Oversampled, Oversampled, Oversampled);

            float[] Continuous = data.GetHostContinuousCopy();
            float[] Initialized = new float[(DimsOversampled.X / 2 + 1) * DimsOversampled.Y * DimsOversampled.Z * 2];

            lock (Image.FFT_CPU_Sync)
                CPU.InitProjector(Dims, Oversampling, Continuous, Initialized, projdim);

            float2[] Initialized2 = Helper.FromInterleaved2(Initialized);
            Data = new Image(Initialized2, DimsOversampled, true);
        }

        public Image Project(int2 dims, float3[] angles)
        {
            PutTexturesOnDevice();
            Image Result = new Image(IntPtr.Zero, new int3(dims.X, dims.Y, angles.Length), true, true);
            Result.Fill(0);
            GPU.ProjectForwardTex(t_DataRe,
                                  t_DataIm,
                                  Result.GetDevice(Intent.Write),
                                  Data.Dims,
                                  dims,
                                  Helper.ToInterleaved(angles),
                                  Oversampling,
                                  (uint)angles.Length);

            return Result;
        }

        public void Project(int2 dims, float3[] angles, Image result)
        {
            PutTexturesOnDevice();
            result.Fill(0);
            GPU.ProjectForwardTex(t_DataRe,
                                  t_DataIm,
                                  result.GetDevice(Intent.Write),
                                  Data.Dims,
                                  dims,
                                  Helper.ToInterleaved(angles),
                                  Oversampling,
                                  (uint)angles.Length);
        }

        public Image Project(int2 dims, float3[] angles, float3[] shifts, float[] globalweights)
        {
            PutTexturesOnDevice();
            Image Result = new Image(IntPtr.Zero, new int3(dims.X, dims.Y, angles.Length), true, true);
            GPU.ProjectForwardShiftedTex(t_DataRe,
                                         t_DataIm,
                                         Result.GetDevice(Intent.Write),
                                         Data.Dims,
                                         dims,
                                         Helper.ToInterleaved(angles),
                                         Helper.ToInterleaved(shifts),
                                         globalweights,
                                         Oversampling,
                                         (uint)angles.Length);

            return Result;
        }

        public Image Project(int3 dims, float3[] angles)
        {
            PutTexturesOnDevice();
            Image Result = new Image(IntPtr.Zero, new int3(dims.X, dims.Y, dims.Z * angles.Length), true, true);
            GPU.ProjectForward3DTex(t_DataRe,
                                    t_DataIm,
                                    Result.GetDevice(Intent.Write),
                                    Data.Dims,
                                    dims,
                                    Helper.ToInterleaved(angles),
                                    Oversampling,
                                    (uint)angles.Length);

            return Result;
        }

        public void Project(int3 dims, float3[] angles, Image result)
        {
            PutTexturesOnDevice();
            result.Fill(0);
            GPU.ProjectForward3DTex(t_DataRe,
                                    t_DataIm,
                                    result.GetDevice(Intent.Write),
                                    Data.Dims,
                                    dims,
                                    Helper.ToInterleaved(angles),
                                    Oversampling,
                                    (uint)angles.Length);
        }

        public Image Project(int3 dims, float3[] angles, float3[] shifts, float[] globalweights)
        {
            if (angles.Length != shifts.Length || angles.Length != globalweights.Length)
                throw new DimensionMismatchException();

            PutTexturesOnDevice();
            Image Result = new Image(IntPtr.Zero, new int3(dims.X, dims.Y, dims.Z * angles.Length), true, true);
            GPU.ProjectForward3DShiftedTex(t_DataRe,
                                           t_DataIm,
                                           Result.GetDevice(Intent.Write),
                                           Data.Dims,
                                           dims,
                                           Helper.ToInterleaved(angles),
                                           Helper.ToInterleaved(shifts),
                                           globalweights,
                                           Oversampling,
                                           (uint)angles.Length);

            return Result;
        }

        public Image ProjectToRealspace(int2 dims, float3[] angles)
        {
            Image Proj = Project(dims, angles);
            Image ProjIFT = Proj.AsIFFT();
            Proj.Dispose();
            ProjIFT.RemapFromFT();
            //ProjIFT.Multiply(dims.X * dims.Y);

            return ProjIFT;
        }

        public Image ProjectToRealspace(int2 dims, float3[] angles, float3[] shifts, float[] globalWeights)
        {
            Image Proj = Project(dims, angles, shifts, globalWeights);
            Image ProjIFT = Proj.AsIFFT();
            Proj.Dispose();
            ProjIFT.RemapFromFT();
            //ProjIFT.Multiply(dims.X * dims.Y);

            return ProjIFT;
        }

        public void BackProject(Image projft, Image projweights, float3[] angles, float3 magnification, float ewaldradius = 0)
        {
            if (!projft.IsFT || !projft.IsComplex || !projweights.IsFT)
                throw new Exception("Input data must be complex (except weights) and in FFTW layout.");

            float[] Angles = Helper.ToInterleaved(angles);
            GPU.ProjectBackward(Data.GetDevice(Intent.ReadWrite),
                                Weights.GetDevice(Intent.ReadWrite),
                                DimsOversampled,
                                projft.GetDevice(Intent.Read),
                                projweights.GetDevice(Intent.Read),
                                projft.DimsSlice,
                                projft.Dims.X / 2,
                                Angles,
                                null,
                                magnification,
                                ewaldradius,
                                Oversampling,
                                false,
                                (uint)angles.Length);
        }

        public void BackProject(Image projft, Image projweights, float3[] angles, float3[] shifts, float[] globalweights)
        {
            if (!projft.IsFT || !projft.IsComplex || !projweights.IsFT)
                throw new Exception("Input data must be complex (except weights) and in FFTW layout.");

            float[] Angles = Helper.ToInterleaved(angles);
            float[] Shifts = Helper.ToInterleaved(shifts);

            GPU.ProjectBackwardShifted(Data.GetDevice(Intent.ReadWrite),
                                       Weights.GetDevice(Intent.ReadWrite),
                                       DimsOversampled,
                                       projft.GetDevice(Intent.Read),
                                       projweights.GetDevice(Intent.Read),
                                       projft.DimsSlice,
                                       projft.Dims.X / 2,
                                       Angles,
                                       Shifts,
                                       globalweights,
                                       Oversampling,
                                       (uint)angles.Length);
        }

        public Image Reconstruct(bool isctf, string symmetry = "C1", int planForw = -1, int planBack = -1, int planForwCTF = -1, int griddingiterations = 10, bool useHostMemory = false)
        {
            if (useHostMemory)
            {
                Data.FreeDevice();
                Weights.FreeDevice();
            }

            Image Reconstruction = useHostMemory ? new Image(Dims, isctf) : new Image(IntPtr.Zero, Dims, isctf);
            GPU.BackprojectorReconstructGPU(Dims,
                                            DimsOversampled,
                                            Oversampling,
                                            useHostMemory ? Data.GetHostPinned(Intent.ReadWrite) : Data.GetDevice(Intent.ReadWrite),
                                            useHostMemory ? Weights.GetHostPinned(Intent.ReadWrite) : Weights.GetDevice(Intent.ReadWrite),
                                            symmetry,
                                            isctf,
                                            useHostMemory ? Reconstruction.GetHostPinned(Intent.Write) : Reconstruction.GetDevice(Intent.Write),
                                            planForw,
                                            planBack,
                                            planForwCTF,
                                            griddingiterations);

            return Reconstruction;
        }

        public void Reconstruct(IntPtr d_reconstruction, bool isctf, string symmetry = "C1", int planForw = -1, int planBack = -1, int planForwCTF = -1, int griddingiterations = 10)
        {
            GPU.BackprojectorReconstructGPU(Dims,
                                            DimsOversampled,
                                            Oversampling,
                                            Data.GetDevice(Intent.Read),
                                            Weights.GetDevice(Intent.Read),
                                            symmetry,
                                            isctf,
                                            d_reconstruction,
                                            planForw,
                                            planBack,
                                            planForwCTF,
                                            griddingiterations);
        }

        public Image ReconstructCPU(bool isctf, string symmetry)
        {
            //float[] ContinuousData = Data.GetHostContinuousCopy();
            //float[] ContinuousWeights = Weights.GetHostContinuousCopy();

            float[] ContinuousResult = new float[Dims.Elements()];

            //Data.FreeDevice();
            //Weights.FreeDevice();

            CPU.BackprojectorReconstruct(Dims, Oversampling, Data.GetDevice(Intent.Read), Weights.GetDevice(Intent.Read), symmetry, isctf, ContinuousResult);

            Image Reconstruction = new Image(ContinuousResult, Dims, isctf);

            return Reconstruction;
        }

        public void MakeWeightsPositive()
        {
            float[][] DataData = Data.GetHost(Intent.ReadWrite);
            float[][] WeightsData = Weights.GetHost(Intent.ReadWrite);

            for (int z = 0; z < DataData.Length; z++)
            {
                float[] D = DataData[z];
                float[] W = WeightsData[z];

                for (int i = 0; i < W.Length; i++)
                {
                    if (W[i] < 0)
                    {
                        W[i] *= -1;
                        //D[i * 2] *= -1;
                        //D[i * 2 + 1] *= -1;
                    }
                }
            }
        }

        public void FreeDevice()
        {
            Data.FreeDevice();
            Weights?.FreeDevice();
            DisposeTextures();
        }

        public void Dispose()
        {
            Data.Dispose();
            Weights?.Dispose();
            DisposeTextures();
        }

        private void DisposeTextures()
        {
            if (t_DataRe > 0)
            {
                GPU.DestroyTexture(t_DataRe, a_DataRe);
                t_DataRe = 0;
                a_DataRe = 0;
            }

            if (t_DataIm > 0)
            {
                GPU.DestroyTexture(t_DataIm, a_DataIm);
                t_DataIm = 0;
                a_DataIm = 0;
            }
        }

        public void PutTexturesOnDevice()
        {
            if (Data == null)
                throw new Exception("No projector data available.");

            if (t_DataRe == 0)
            {
                ulong[] Textures = new ulong[2];
                ulong[] Arrays = new ulong[2];
                GPU.CreateTexture3DComplex(Data.GetDevice(Intent.Read), Data.DimsEffective, Textures, Arrays, false);

                t_DataRe = Textures[0];
                t_DataIm = Textures[1];
                a_DataRe = Arrays[0];
                a_DataIm = Arrays[1];

                Data.FreeDevice();
            }
        }

        public void WriteMRC(string path)
        {
            Image DataRe = Data.AsReal();
            DataRe.FreeDevice();
            Image DataIm = Data.AsImaginary();
            DataIm.FreeDevice();

            Image Combined = Image.Stack(new[] { DataRe, DataIm, Weights });
            DataRe.Dispose();
            DataIm.Dispose();

            Combined.WriteMRC(path, Oversampling, true);
            Combined.Dispose();
        }

        public static void GetPlans(int3 dims, int oversampling, out int planForw, out int planBack, out int planForwCTF)
        {
            int Oversampled = dims.X * oversampling;
            int3 DimsOversampled = new int3(Oversampled, Oversampled, Oversampled);

            planForw = GPU.CreateFFTPlan(DimsOversampled, 1);
            planBack = GPU.CreateIFFTPlan(DimsOversampled, 1);
            planForwCTF = GPU.CreateFFTPlan(dims, 1);
        }

        public static Projector FromFile(string path)
        {
            Image Combined = Image.FromFile(path);
            int Oversampling = (int)(Combined.PixelSize + 0.1f);
            int3 DimsOversampled = new int3(Combined.Dims.Y);
            int3 Dims = (DimsOversampled - 3) / Oversampling;

            Projector Result = new Projector(Dims, Oversampling);

            float[][] CombinedData = Combined.GetHost(Intent.Read);
            float[][] DataData = Result.Data.GetHost(Intent.Write);
            float[][] WeightsData = Result.Weights.GetHost(Intent.Write);

            for (int z = 0; z < DimsOversampled.Z; z++)
            {
                int ElementsSlice = (int)DimsOversampled.Slice().ElementsFFT();

                for (int i = 0; i < ElementsSlice; i++)
                {
                    DataData[z][i * 2 + 0] = CombinedData[z][i];
                    DataData[z][i * 2 + 1] = CombinedData[DimsOversampled.Z + z][i];
                    WeightsData[z][i] = CombinedData[DimsOversampled.Z * 2 + z][i];
                }
            }

            Combined.Dispose();

            return Result;
        }
    }
}
