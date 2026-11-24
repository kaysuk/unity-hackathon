using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace Hackathon.Tests
{
    public sealed class NimbAttackModelTests
    {
        private const BindingFlags Methods = BindingFlags.NonPublic | BindingFlags.Public;
        private object model;
        private MethodInfo predict;
        private MethodInfo choose;
        private Type attackType;

        [SetUp]
        public void LoadGameWeights()
        {
            var modelType = Type.GetType("NimbAttackModel, Assembly-CSharp", true);
            model = modelType.GetMethod("Load", Methods | BindingFlags.Static).Invoke(null, null);
            Assert.IsNotNull(model, "The model packaged in Resources must load.");
            predict = modelType.GetMethod("Predict", Methods | BindingFlags.Instance);
            choose = modelType.GetMethod("Choose", Methods | BindingFlags.Instance);
            attackType = predict.GetParameters()[0].ParameterType;
        }

        private float Predict(string attack, float distance, int coverResponses)
        {
            return (float)predict.Invoke(model, new object[] {
                Enum.Parse(attackType, attack), distance, coverResponses, false
            });
        }

        [Test]
        public void RepeatedTripsToCoverReverseThePreferenceOnTheSameOpenGround()
        {
            Assert.Greater(Predict("HaloWave", 8f, 0), Predict("EyeSeal", 8f, 0));
            Assert.Greater(Predict("EyeSeal", 8f, 2), Predict("HaloWave", 8f, 2));
            Assert.Greater(Predict("HandTouch", 3f, 0), Predict("HandTouch", 18f, 0));
        }

        [Test]
        public void WeightedChoiceCanReachEachEligibleAttackAndHonoursExclusions()
        {
            foreach (bool allAvailable in new[] { true, false })
            {
                object excluded = allAvailable ? null : Enum.Parse(attackType, "EyeSeal");
                var seen = new HashSet<string>();
                for (int i = 0; i <= 200; i++)
                {
                    var selected = choose.Invoke(model, new object[] {
                        8f, 1, false, allAvailable, allAvailable, excluded, i / 200f
                    });
                    seen.Add(selected.ToString());
                }

                var expected = allAvailable ? Enum.GetNames(attackType) : new[] { "HaloWave", "LightArrows" };
                CollectionAssert.AreEquivalent(expected, seen);
            }
        }
    }
}
