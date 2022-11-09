using UnityEngine;

namespace ResearchPowl
{
	internal static class FastGUI
	{
		static Internal_DrawTextureArguments drawArguments = new Internal_DrawTextureArguments
		{
			leftBorder = 0,
			rightBorder = 0,
			topBorder = 0,
			bottomBorder = 0,
			leftBorderColor = Color.white,
			topBorderColor = Color.white,
			rightBorderColor = Color.white,
			bottomBorderColor = Color.white,
			cornerRadiuses = new Vector4(0f, 0f, 0f, 0f),
			smoothCorners = false,
			sourceRect = new Rect(0f, 0f, 1f, 1f),
			mat = GUI.roundedRectMaterial,
			borderWidths = Vector4.zero
		};

		public static void DrawTextureFast(Rect position, Texture image, Color color)
		{
			drawArguments.screenRect = position;
			drawArguments.texture = image;
			drawArguments.color = color;
			Graphics.Internal_DrawTexture(ref drawArguments);
		}

		public static void DrawTextureFastWithCoords(Rect position, Texture image, Color color, Rect rect)
		{
			drawArguments.screenRect = position;
			drawArguments.texture = image;
			drawArguments.color = color;
			drawArguments.sourceRect = rect;
			Graphics.Internal_DrawTexture(ref drawArguments);
		}

		public static bool ButtonImageFast(Rect butRect, int controlID, Event current, EventType eventType, Texture2D tex)
		{
			DrawTextureFast(butRect, tex, ResourceBank.colorWhite);

			if (eventType == EventType.MouseDown)
			{
				bool flag = GUIUtility.HitTest(butRect, current);
				if (flag)
				{
					GUI.GrabMouseControl(controlID);
					current.Use();
				}
			}
			if (eventType == EventType.MouseUp)
			{
				bool flag2 = GUI.HasMouseControl(controlID);
				if (flag2)
				{
					GUI.ReleaseMouseControl();
					current.Use();
					bool flag3 = GUIUtility.HitTest(butRect, current);
					if (flag3)
					{
						GUI.changed = true;
						return true;
					}
				}
			}
			return false;
		}
	}
}