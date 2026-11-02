using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hackathon.Tests
{
    public class ProjectConfigurationTests
    {
        [Test]
        public void EditorVersionMatchesCommittedProjectVersion()
        {
            var root = Directory.GetParent(Application.dataPath).FullName;
            var line = File.ReadLines(Path.Combine(root, "ProjectSettings/ProjectVersion.txt"))
                .First(value => value.StartsWith("m_EditorVersion:"));
            Assert.That(Application.unityVersion, Is.EqualTo(line.Split(':')[1].Trim()));
        }

        [Test]
        public void EnabledBuildScenesExist()
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
            Assert.That(scenes, Is.Not.Empty, "Configure at least one enabled build scene.");
            foreach (var scene in scenes)
            {
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path),
                    Is.Not.Null, $"Build scene is missing: {scene.path}");
            }
        }

        [Test]
        public void UnityAssetsRemainCompatibleWithGit()
        {
            Assert.That(EditorSettings.serializationMode, Is.EqualTo(SerializationMode.ForceText));
            Assert.That(VersionControlSettings.mode, Is.EqualTo("Visible Meta Files"));
        }

        [Test]
        public void ActiveQualityUsesUniversalRenderPipeline()
        {
            var pipeline = QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline;
            Assert.That(pipeline, Is.Not.Null,
                "The active quality level must resolve to a render pipeline asset.");
            Assert.That(pipeline.GetType().Name,
                Is.EqualTo("UniversalRenderPipelineAsset"));
        }
    }
}
