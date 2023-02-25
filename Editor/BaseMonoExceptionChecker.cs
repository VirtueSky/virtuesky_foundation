﻿using System;
using System.Reflection;
using Pancake;
using UnityEngine;

namespace PancakeEditor
{
    public class BaseMonoExceptionChecker
    {
        private const BindingFlags METHOD_FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        public void CheckForExceptions()
        {
            var subclassTypes = typeof(BaseMono).GetAllSubClass(type => type.IsSubclassOf(typeof(BaseMono)));

            foreach (var type in subclassTypes)
            {
                var methods = type.GetMethods(METHOD_FLAGS);

                foreach (var method in methods)
                {
                    if (method.Name == "OnEnable")
                    {
                        Debug.LogException(new Exception($"{GetExceptionBaseText("OnEnable", type.Name)}" + "protected override void ".TextColor(Uniform.Blue) +
                                                         "OnEnabled()".TextColor(Uniform.Orange)));
                    }

                    if (method.Name == "OnDisable")
                    {
                        Debug.LogException(new Exception($"{GetExceptionBaseText("OnDisable", type.Name)}" + "protected override void ".TextColor(Uniform.Blue) +
                                                         "OnDisabled()".TextColor(Uniform.Orange)));
                    }

                    if (method.Name == "Update")
                    {
                        Debug.LogWarning(GetWarningBaseText(method.Name, "Tick()", type.Name));
                    }

                    if (method.Name == "FixedUpdate")
                    {
                        Debug.LogWarning(GetWarningBaseText(method.Name, "FixedTick()", type.Name));
                    }

                    if (method.Name == "LateUpdate")
                    {
                        Debug.LogWarning(GetWarningBaseText(method.Name, "LateTick()", type.Name));
                    }
                }
            }
        }

        private string GetExceptionBaseText(string methodName, string className)
        {
            var classNameColored = className.TextColor(Uniform.Red);
            var monoCacheNameColored = nameof(BaseMono).TextColor(Uniform.Orange);
            var methodNameColored = methodName.TextColor(Uniform.Red);

            return $"{methodNameColored} can't be implemented in subclass {classNameColored} of {monoCacheNameColored}. Use ";
        }

        private string GetWarningBaseText(string methodName, string recommendedMethod, string className)
        {
            var coloredClass = className.TextColor(Uniform.Orange);
            var monoCacheNameColored = nameof(BaseMono).TextColor(Uniform.Orange);
            var coloredMethod = methodName.TextColor(Uniform.Orange);

            var coloredRecommendedMethod = "protected override void ".TextColor(Uniform.Blue) + recommendedMethod.TextColor(Uniform.Orange);
            return $"It is recommended to replace {coloredMethod} method with {coloredRecommendedMethod} " + $"in subclass {coloredClass} of {monoCacheNameColored}";
        }
    }
}