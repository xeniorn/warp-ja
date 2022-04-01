﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Warp.Tools;

namespace Warp
{
    [Serializable]
    public abstract class ProcessingOptionsBase : WarpBase
    {
        [WarpSerializable]
        public decimal PixelSizeX { get; set; }
        [WarpSerializable]
        public decimal PixelSizeY { get; set; }
        [WarpSerializable]
        public decimal PixelSizeAngle { get; set; }
        [WarpSerializable]
        public decimal BinTimes { get; set; }
        [WarpSerializable]
        public int EERGroupFrames { get; set; }
        [WarpSerializable]
        public string GainPath { get; set; }
        [WarpSerializable]
        public string GainHash { get; set; }
        [WarpSerializable]
        public bool GainFlipX { get; set; }
        [WarpSerializable]
        public bool GainFlipY { get; set; }
        [WarpSerializable]
        public bool GainTranspose { get; set; }
        [WarpSerializable]
        public string DefectsPath { get; set; }
        [WarpSerializable]
        public string DefectsHash { get; set; }
        [WarpSerializable]
        public float3 Dimensions { get; set; }

        public decimal PixelSizeMean => (PixelSizeX + PixelSizeY) * 0.5M;
        public decimal PixelSizeDelta => (PixelSizeX - PixelSizeY) * 0.5M;
        public decimal DownsampleFactor => (decimal)Math.Pow(2.0, (double)BinTimes);
        public decimal BinnedPixelSizeX => PixelSizeX * DownsampleFactor;
        public decimal BinnedPixelSizeY => PixelSizeY * DownsampleFactor;
        public decimal BinnedPixelSizeMean => PixelSizeMean * DownsampleFactor;
        public decimal BinnedPixelSizeDelta => PixelSizeDelta * DownsampleFactor;
        

        protected bool Equals(ProcessingOptionsBase other)
        {
            return PixelSizeX == other.PixelSizeX &&
                   PixelSizeY == other.PixelSizeY &&
                   PixelSizeAngle == other.PixelSizeAngle &&
                   (string.IsNullOrEmpty(GainHash) ? GainPath == other.GainPath : GainHash == other.GainHash) &&
                   GainFlipX == other.GainFlipX &&
                   GainFlipY == other.GainFlipY &&
                   GainTranspose == other.GainTranspose &&
                   (string.IsNullOrEmpty(DefectsHash) ? DefectsPath == other.DefectsPath : DefectsHash == other.DefectsHash) &&
                   BinTimes == other.BinTimes &&
                   (EERGroupFrames == 0 || other.EERGroupFrames == 0 || EERGroupFrames == other.EERGroupFrames);
        }

        public static bool operator ==(ProcessingOptionsBase left, ProcessingOptionsBase right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ProcessingOptionsBase left, ProcessingOptionsBase right)
        {
            return !Equals(left, right);
        }
    }
}
