#if UNITY_EDITOR
using Cysharp.Threading.Tasks;
using JojoP.AOT;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace JojoP.EditorTools
{
    /// <summary>
    /// 编辑器 Play：Main 不在 Build Profile 里，必须用 LoadSceneAsyncInPlayMode。
    /// </summary>
    [InitializeOnLoad]
    static class JojoPEditorPlayBoot
    {
        static JojoPEditorPlayBoot()
        {
            AppLauncher.LoadSceneInEditorPlay = LoadMain;
        }

        static async UniTask LoadMain(string sceneName)
        {
            string path = $"Assets/Scenes/{sceneName}.unity";
            var op = EditorSceneManager.LoadSceneAsyncInPlayMode(
                path, new LoadSceneParameters(LoadSceneMode.Single));
            if (op == null)
                throw new System.Exception($"LoadSceneAsyncInPlayMode 失败: {path}");
            while (!op.isDone)
                await UniTask.Yield();
        }
    }
}
#endif
