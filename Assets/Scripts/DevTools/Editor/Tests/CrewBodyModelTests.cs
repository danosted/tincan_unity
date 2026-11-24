#nullable enable
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TinCan.DevTools.Editor.Tests
{
    /// <summary>
    /// The built crew body (TinCan > Dev > Crew Body > Build Assets, from .tools/blender/crew_body.py) faces the player's
    /// forward, +Z, with its left hand on the left (-X), and stands 2 m tall on its root.
    /// </summary>
    public class CrewBodyModelTests
    {
        private GameObject? _body;

        [TearDown]
        public void TearDown()
        {
            if (_body != null) Object.DestroyImmediate(_body);
        }

        private Animator Body()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrewBodyAssetBuilder.PrefabPath)
                         ?? throw new AssertionException($"missing {CrewBodyAssetBuilder.PrefabPath}: run TinCan > Dev > Crew Body > Build Assets");
            _body = Object.Instantiate(prefab);
            return _body.GetComponentInChildren<Animator>();
        }

        [Test]
        public void TheBody_FacesForward_LeftOnTheLeft()
        {
            var body = Body();
            Vector3 foot = body.GetBoneTransform(HumanBodyBones.LeftFoot).position;
            Vector3 toes = body.GetBoneTransform(HumanBodyBones.LeftToes).position;
            Vector3 leftHand = body.GetBoneTransform(HumanBodyBones.LeftHand).position;

            Assert.That(toes.z - foot.z, Is.GreaterThan(0.05f), "the toes point forward (+Z)");
            Assert.That(leftHand.x, Is.LessThan(-0.3f), "the left hand is on the left (-X)");
        }

        [Test]
        public void TheBody_StandsOnItsRoot_TwoMetresTall()
        {
            var body = Body();
            Vector3 head = body.GetBoneTransform(HumanBodyBones.Head).position;
            Vector3 foot = body.GetBoneTransform(HumanBodyBones.LeftFoot).position;

            Assert.That(foot.y, Is.InRange(0f, 0.2f), "the ankle sits just above the root");
            Assert.That(head.y, Is.InRange(1.5f, 1.8f), "the head bone starts at the neck of a 2 m body");
        }
    }
}
