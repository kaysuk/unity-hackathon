using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hackathon.Tests
{
    public class IvanSidearmClipTests
    {
        private const string Sidearm = "Assets/Character/Ivan_Karamazov/Sidearm/";

        [TestCase("LeftHand")]
        [TestCase("RightHand")]
        public void ThumbPoseStaysInRangeAndReversesWhenStowing(string hand)
        {
            var draw = AssetDatabase.LoadAssetAtPath<AnimationClip>(Sidearm + "Draw.anim");
            var stow = AssetDatabase.LoadAssetAtPath<AnimationClip>(Sidearm + "Stow.anim");
            Assert.That(draw, Is.Not.Null);
            Assert.That(stow, Is.Not.Null);

            foreach (var muscle in new[] { "1 Stretched", "2 Stretched", "3 Stretched", "Spread" })
            {
                string name = hand + ".Thumb." + muscle;
                var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), name);
                var drawCurve = AnimationUtility.GetEditorCurve(draw, binding);
                var stowCurve = AnimationUtility.GetEditorCurve(stow, binding);
                Assert.That(drawCurve, Is.Not.Null, name);
                Assert.That(stowCurve, Is.Not.Null, name);

                int frames = Mathf.CeilToInt(Mathf.Max(draw.length * draw.frameRate, stow.length * stow.frameRate));
                for (int frame = 0; frame <= frames; frame++)
                {
                    float phase = (float)frame / frames;
                    float drawn = drawCurve.Evaluate(phase * draw.length);
                    float stowed = stowCurve.Evaluate((1f - phase) * stow.length);
                    Assert.That(drawn, Is.InRange(-1f, 1f), name);
                    Assert.That(stowed, Is.InRange(-1f, 1f), name);
                    Assert.That(stowed, Is.EqualTo(drawn).Within(0.001f),
                        $"{name} changes on reversing the gesture at phase {phase:F2}.");
                }
            }
        }
    }
}
