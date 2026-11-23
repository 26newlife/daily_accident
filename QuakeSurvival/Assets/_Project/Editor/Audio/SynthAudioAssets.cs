using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace QuakeSurvival.EditorTools.AudioSynth
{
    /// <summary>
    /// 합성한 소리를 WAV(16비트 모노)로 Assets/_Project/Audio/Synth 에 저장하고 AudioClip으로 불러온다.
    /// 이미 같은 이름의 파일이 있으면 다시 만들지 않는다. 진짜 효과음으로 바꾸려면 같은 이름의 파일로 덮어쓰면 된다.
    /// </summary>
    internal static class SynthAudioAssets
    {
        public const string Dir = "Assets/_Project/Audio/Synth";

        /// <summary>코드로 만든 파일 표시(임포터 userData 앞부분). 뒤에 만들 당시 파일 해시가 붙는다.</summary>
        public const string SynthMark = "synth:";

        /// <summary>파일 내용의 MD5 해시(16진수). 내용이 바뀌었는지 확인하는 지문으로 쓴다.</summary>
        public static string FileHash(string path)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            using (var stream = File.OpenRead(path))
            {
                return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "");
            }
        }

        public static AudioClip Get(string name, Func<float[]> synth, bool loop)
        {
            string path = $"{Dir}/{name}.wav";
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Dir);
                WriteWav(path, synth());
                AssetDatabase.ImportAsset(path);
                ConfigureImport(path, loop);
            }
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) Debug.LogError($"[SynthAudioAssets] 소리를 불러오지 못했습니다: {path}");
            return clip;
        }

        private static void ConfigureImport(string path, bool loop)
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) return;
            importer.forceToMono = true;
            var settings = importer.defaultSampleSettings;
            settings.loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
            importer.userData = SynthMark + FileHash(path);
            importer.SaveAndReimport();
        }

        private static void WriteWav(string path, float[] samples)
        {
            using (var stream = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(stream))
            {
                int dataBytes = samples.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataBytes);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);                       // PCM
                w.Write((short)1);                       // 모노
                w.Write(SynthDsp.Rate);
                w.Write(SynthDsp.Rate * 2);              // byte rate
                w.Write((short)2);                       // block align
                w.Write((short)16);                      // bits per sample
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(dataBytes);
                foreach (float s in samples)
                {
                    w.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
                }
            }
        }
    }
}
