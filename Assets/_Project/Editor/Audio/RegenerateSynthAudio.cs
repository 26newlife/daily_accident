using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace QuakeSurvival.EditorTools.AudioSynth
{
    /// <summary>
    /// 메뉴: 합성 효과음을 지우고 지금 코드로 다시 만든 뒤 씬을 다시 짓는다.
    /// 만들 당시와 내용이 같은(= 사람이 덮어쓰지 않은) 파일만 지우므로, 직접 넣은 진짜 효과음은 그대로 남는다.
    /// </summary>
    public static class RegenerateSynthAudio
    {
        [MenuItem("QuakeSurvival/Regenerate Synth Audio")]
        private static void Menu()
        {
            bool ok = EditorUtility.DisplayDialog("합성 효과음 다시 만들기",
                "코드로 만든 효과음을 지우고 다시 만든 뒤 TestRoom 씬을 다시 짓습니다.\n" +
                "직접 바꿔 넣은 효과음 파일은 지우지 않습니다. 씬에서 직접 고친 내용은 사라집니다. 계속할까요?",
                "다시 만들기", "취소");
            if (ok) Debug.Log(Run());
        }

        /// <summary>합성 파일을 지우고 씬을 다시 지어 새 소리를 만든다. 결과 요약 문자열을 돌려준다.</summary>
        public static string Run()
        {
            List<string> removed = DeleteUntouchedSynthFiles(out List<string> kept);
            string build = SceneBuilder.BuildAll();
            return $"지운 합성 파일 {removed.Count}개, 건너뛴 파일 {kept.Count}개" +
                   (kept.Count > 0 ? $" ({string.Join(", ", kept)})" : "") + $" / {build}";
        }

        private static List<string> DeleteUntouchedSynthFiles(out List<string> kept)
        {
            var removed = new List<string>();
            kept = new List<string>();
            if (!Directory.Exists(SynthAudioAssets.Dir)) return removed;

            foreach (string file in Directory.GetFiles(SynthAudioAssets.Dir, "*.wav"))
            {
                string path = file.Replace('\\', '/');
                if (IsUntouchedSynth(path))
                {
                    AssetDatabase.DeleteAsset(path);
                    removed.Add(Path.GetFileName(path));
                }
                else
                {
                    kept.Add(Path.GetFileName(path));
                }
            }
            AssetDatabase.Refresh();
            return removed;
        }

        /// <summary>코드로 만든 표시가 있고, 지금 파일 해시가 만들 당시와 같으면 true.</summary>
        private static bool IsUntouchedSynth(string path)
        {
            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (importer == null) return false;
            string data = importer.userData ?? "";
            if (!data.StartsWith(SynthAudioAssets.SynthMark)) return false;
            string recorded = data.Substring(SynthAudioAssets.SynthMark.Length);
            return recorded == SynthAudioAssets.FileHash(path);
        }
    }
}
