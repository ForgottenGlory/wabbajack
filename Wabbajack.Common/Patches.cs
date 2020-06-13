﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using RocksDbSharp;

namespace Wabbajack.Common
{
    public static partial class Utils
    {

        private static RocksDb? _patchCache;
        private static void InitPatches()
        {
            var options = new DbOptions().SetCreateIfMissing(true); 
            _patchCache = RocksDb.Open(options, (string)Consts.LocalAppDataPath.Combine("PatchCache.rocksDb"));
        }


        private static byte[] PatchKey(Hash src, Hash dest)
        {
            var arr = new byte[16];
            Array.Copy(BitConverter.GetBytes((ulong)src), 0, arr, 0, 8);
            Array.Copy(BitConverter.GetBytes((ulong)dest), 0, arr, 8, 8);
            return arr;
        }
        public static async Task CreatePatchCached(byte[] a, byte[] b, Stream output)
        {
            var dataA = a.xxHash();
            var dataB = b.xxHash();
            var key = PatchKey(dataA, dataB);
            var found = _patchCache!.Get(key);

            if (found != null)
            {
                await output.WriteAsync(found);
                return;
            }

            await using var patch = new MemoryStream();

            Status("Creating Patch");
            OctoDiff.Create(a, b, patch);
            
            _patchCache.Put(key, patch.ToArray());
            patch.Position = 0;

            await patch.CopyToAsync(output);
        }

        public static async Task CreatePatchCached(Stream srcStream, Hash srcHash, FileStream destStream, Hash destHash,
            Stream patchOutStream)
        {
            var key = PatchKey(srcHash, destHash);
            var patch = _patchCache!.Get(key);
            if (patch != null)
            {
                await patchOutStream.WriteAsync(patch);
                return;
            }
            
            Status("Creating Patch");
            await using var sigStream = new MemoryStream();
            await using var patchStream = new MemoryStream();
            OctoDiff.Create(srcStream, destStream, sigStream, patchStream);
            _patchCache.Put(key, patchStream.ToArray());

            patchStream.Position = 0;
            await patchStream.CopyToAsync(patchOutStream);
        }

        public static bool TryGetPatch(Hash foundHash, Hash fileHash, [MaybeNullWhen(false)] out byte[] ePatch)
        {
            var key = PatchKey(foundHash, fileHash);
            var patch = _patchCache!.Get(key);

            if (patch != null)
            {
                ePatch = patch;
                return true;
            }

            ePatch = null;
            return false;

        }

        public static void ApplyPatch(Stream input, Func<Stream> openPatchStream, Stream output)
        {
            using var ps = openPatchStream();
            using var br = new BinaryReader(ps);
            var bytes = br.ReadBytes(8);
            var str = Encoding.ASCII.GetString(bytes);
            switch (str)
            {
                case "BSDIFF40":
                    BSDiff.Apply(input, openPatchStream, output);
                    return;
                case "OCTODELT":
                    OctoDiff.Apply(input, openPatchStream, output);
                    return;
                default:
                    throw new Exception($"No diff dispatch for: {str}");
            }
        }
    }
}