// HairBaker_FIX.cs - Исправление для аниме-волос
// Добавьте этот файл рядом с HairBaker.cs.
// Он переопределяет умолчания: если strandSides не задан (0),
// используем 4 (аниме-пряди) вместо 6 (круглые трубки).
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using static CharacterEditor.Hair.Proc.HairBaker;

namespace CharacterEditor.Hair.Proc
{
    public static class HairBakerFixes
    {
        /// <summary>
        /// Исправленная версия Bake с аниме-умолчаниями.
        /// Используйте HairBakerFixes.Bake() вместо HairBaker.Bake()
        /// если strandSides не заданы в ассете.
        /// </summary>
        public static BakeResult Bake(HairPieceDefinitionProc def, HairDna dna, int lod = 0)
        {
            // Принудительно устанавливаем strandSides=4 если не задано
            if (def.strandSides <= 0 || def.strandSides > 6)
            {
                // Проверяем, дреды ли это (Desmond)
                string id = (def.id ?? def.name ?? "").ToLowerInvariant();
                if (id.Contains("desmond"))
                {
                    if (def.strandSides == 0) def.strandSides = 6;
                }
                else
                {
                    def.strandSides = 4; // аниме-пряди
                }
            }

            // Устанавливаем width/depth если не заданы
            if (def.strandWidthScale < 0.01f)
                def.strandWidthScale = 2.5f;
            if (def.strandDepthScale < 0.01f)
                def.strandDepthScale = 0.4f;

            // Separation
            if (!def.enableStrandSeparation)
            {
                def.enableStrandSeparation = true;
                def.strandSeparationRadius = 0.024f;
                def.strandSeparationStrength = 0.55f;
            }

            return HairBaker.Bake(def, dna, lod);
        }
    }
}