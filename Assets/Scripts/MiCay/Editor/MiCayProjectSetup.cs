using UnityEditor;

namespace MiCay.EditorTools
{
    [InitializeOnLoad]
    static class MiCayProjectSetup
    {
        static MiCayProjectSetup()
        {
            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.AutoRotation ||
                PlayerSettings.allowedAutorotateToPortrait ||
                PlayerSettings.allowedAutorotateToPortraitUpsideDown ||
                !PlayerSettings.allowedAutorotateToLandscapeLeft ||
                !PlayerSettings.allowedAutorotateToLandscapeRight)
            {
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
                PlayerSettings.allowedAutorotateToPortrait = false;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
                PlayerSettings.allowedAutorotateToLandscapeRight = true;
                UnityEngine.Debug.Log("[MiCay] Đã đặt chế độ màn hình ngang (Landscape) trong Player Settings.");
            }

            bool reloadDisabled = EditorSettings.enterPlayModeOptionsEnabled &&
                                  (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0;
            if (reloadDisabled && EditorApplication.isPlaying)
            {
                UnityEngine.Debug.LogWarning("[MiCay] Code vừa được biên dịch lại trong lúc Play nên dữ liệu game đã mất. Tự dừng Play — bấm Play lại để chơi tiếp.");
                EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
            }
        }

        [MenuItem("Mì Cay/Xóa dữ liệu lưu")]
        static void DeleteSave()
        {
            var dir = UnityEngine.Application.persistentDataPath;
            System.IO.File.Delete(System.IO.Path.Combine(dir, "micay_save.json"));
            System.IO.File.Delete(System.IO.Path.Combine(dir, "micay_chapter_start.json"));
            UnityEngine.Debug.Log("[MiCay] Đã xóa dữ liệu lưu.");
        }
    }
}
