﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitMiracle.LibTiff.Classic;
using Warp.Tools;

/*
LICENSE NOTE FOR TIFF LIBRARY:

LibTiff.Net
Copyright (c) 2008-2011, Bit Miracle

All rights reserved.

Redistribution and use in source and binary forms, with or without modification, 
are permitted provided that the following conditions are met:

Redistributions of source code must retain the above copyright notice, this list 
of conditions and the following disclaimer.

Redistributions in binary form must reproduce the above copyright notice, this 
list of conditions and the following disclaimer in the documentation and/or 
other materials provided with the distribution.

Neither the name of the Bit Miracle nor the names of its contributors may be used 
to endorse or promote products derived from this software without specific prior 
written permission.
*/

namespace Warp.Headers
{
    public class HeaderEER : MapHeader
    {
        public static int SuperResolution = 1;
        public static int GroupNFrames = 10;

        public int3 DimensionsUngrouped = new int3(1);

        private string Path;

        public HeaderEER()
        {
        }

        public HeaderEER(string path, Stream stream = null)
        {
            Path = path;

            if (stream == null)
                stream = File.OpenRead(path);

            Tiff Image = Tiff.ClientOpen("inmemory", "r", stream, new TiffStream());
            {
                {
                    FieldValue[] value = Image.GetField(TiffTag.IMAGEWIDTH);
                    DimensionsUngrouped.X = value[0].ToInt();
                }
                {
                    FieldValue[] value = Image.GetField(TiffTag.IMAGELENGTH);
                    DimensionsUngrouped.Y = value[0].ToInt();
                }
                {
                    DimensionsUngrouped.Z = Image.NumberOfDirectories();
                }
            }

            if (stream.GetType() != typeof(MemoryStream))
                stream.Close();

            Dimensions = new int3(DimensionsUngrouped.X,
                                  DimensionsUngrouped.Y,
                                  DimensionsUngrouped.Z / GroupNFrames);
        }

        public override void Write(BinaryWriter writer)
        {
            throw new NotImplementedException();
        }

        public override Type GetValueType()
        {
            return typeof(float);
        }

        public override void SetValueType(Type t)
        {
            throw new NotImplementedException();
        }
    }
}
