using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Globalization;
using System.Windows.Forms;
using System.Drawing;

namespace Noxico
{
	public class Inventory
	{
		//TODO: <del>Rewrite to use UIManager</del> Adapt the Drop key to, it's the only one left.
		private static int selection = 0;
		//Split up the Dictionary for easier access to both halves. It was a silly setup anyway.
		private static List<Token> inventoryTokens = new List<Token>();
		private static List<InventoryItem> inventoryItems = new List<InventoryItem>();
		private static UIList itemList;
		private static UILabel howTo, itemDesc;

		private static void TryUse(Character character, Token token, InventoryItem chosen)
		{
			//Subscreens.PreviousScreen.Push(NoxicoGame.Subscreen);
			Subscreens.PreviousScreen.Push(NoxicoGame.Subscreen);
			chosen.Use(character, token);
			Subscreens.Redraw = true;
		}

		private static void TryDrop(BoardChar boardchar, Token token, InventoryItem chosen)
		{
			//Subscreens.PreviousScreen.Push(NoxicoGame.Subscreen);
			Subscreens.PreviousScreen.Push(NoxicoGame.Subscreen);
			if (token.HasToken("equipped"))
				try
				{
					chosen.Unequip(boardchar.Character, token);
				}
				catch (ItemException x)
				{
					MessageBox.Message(x.Message);
				}
			if (!token.HasToken("equipped"))
			{
				chosen.Drop(boardchar, token);
				NoxicoGame.HostForm.Noxico.CurrentBoard.Update();
				NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
				NoxicoGame.HostForm.Noxico.CurrentBoard.Draw();
				NoxicoGame.Sound.PlaySound("Put Item");
				MessageBox.Message("Dropped " + chosen.ToString(token, true, true) + ".");
				/*
				Subscreens.PreviousScreen.Clear();
				NoxicoGame.Mode = UserMode.Walkabout;
				NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
				Subscreens.FirstDraw = true;
				NoxicoGame.AddMessage("Dropped " + chosen.ToString(token, true, true) + ".");
				*/
			}
		}

		public static void Handler()
		{
			var host = NoxicoGame.HostForm;
			var keys = NoxicoGame.KeyMap;
			var trig = NoxicoGame.KeyTrg;
			var player = NoxicoGame.HostForm.Noxico.Player;
			if (!player.Character.HasToken("items") || player.Character.GetToken("items").Tokens.Count == 0)
			{
				MessageBox.Message("You are carrying nothing.", true);
				Subscreens.PreviousScreen.Clear();
				Subscreens.FirstDraw = true;
				return;
			}

			if (Subscreens.FirstDraw)
			{
				UIManager.Initialize();
				Subscreens.FirstDraw = false;
				NoxicoGame.ClearKeys();
				Subscreens.Redraw = true;
			}
			if (Subscreens.Redraw)
			{
				Subscreens.Redraw = false;

				inventoryTokens.Clear();
				inventoryItems.Clear();
				var itemTexts = new List<string>();
				foreach (var carriedItem in player.Character.GetToken("items").Tokens)
				{
					var find = NoxicoGame.KnownItems.Find(x => x.ID == carriedItem.Name);
					if (find == null)
						continue;
					inventoryTokens.Add(carriedItem);
					inventoryItems.Add(find);

					var item = find;
					var sigil = "";
					var carried = carriedItem;
					if (item.HasToken("equipable"))
						sigil += "<c" + (carried.HasToken("equipped") ? "Navy" : "Gray") + ">W";
					if (carried.HasToken("unidentified"))
						sigil += "<cSilver>?";
#if DEBUG
					if (carried.HasToken("cursed"))
						sigil += "<c" + (carried.GetToken("cursed").HasToken("known") ? "Magenta" : "Purple") + ">C";
#else
					if (carried.HasToken("cursed") && carried.GetToken("cursed").HasToken("known"))
						sigil += "<c13>C";
#endif
					var itemString = item.ToString(carried);
					if (itemString.Length > 30)
						itemString = item.ToString(carried, false, false); 
					itemTexts.Add(itemString.PadRight(30) + "<cBlack> " + sigil);
				}
				var height = inventoryItems.Count;
				if (height > 13)
					height = 13;
				if (selection >= inventoryItems.Count)
					selection = inventoryItems.Count - 1;

				//descriptionWindow = new UIWindow(string.Empty) { Left = 2, Top = 17, Width = 76, Height = 6, Background = Color.Black, Foreground = Color.Navy, Title = Color.Silver };
				//description = new UILabel("") { Left = 4, Top = 18, Width = 72, Height = 4, Foreground = Color.Silver, Background = Color.Black };

				UIManager.Elements.Add(new UIWindow("Your inventory") { Left = 1, Top = 1, Width = 37, Height = 2 + height, Background = Color.Black, Foreground = Color.Magenta });
				UIManager.Elements.Add(new UIWindow(string.Empty)  { Left = 2, Top = 17, Width = 76, Height = 6, Background = Color.Black, Foreground = Color.Navy, Title = Color.Silver });
				howTo = new UILabel("") { Left = 0, Top = 24, Width = 79, Height = 1, Background = Color.Black, Foreground = Color.Silver };
				itemDesc = new UILabel("") { Left = 4, Top = 18, Width = 72, Height = 4, Foreground = Color.Silver, Background = Color.Black };
				itemList = new UIList("", null, itemTexts) { Left = 2, Top = 2, Width = 36, Height = height, Background = Color.Black, Foreground = Color.Gray, Index = selection };
				itemList.Change = (s, e) =>
				{
					selection = itemList.Index;

					var t = inventoryTokens[itemList.Index];
					var i = inventoryItems[itemList.Index];
					var r = string.Empty;
					var d = i.HasToken("description") && !t.HasToken("unidentified") ? i.GetToken("description").Text : "This is " + i.ToString() + ".";

					d = Toolkit.Wordwrap(d, itemDesc.Width);

					if (i.ID == "book")
						r = "Press Enter to read.";
					else if (i.HasToken("equipable"))
					{
						if (t.HasToken("equipped"))
						{
							if (t.Path("cursed/known") != null)
								r = "Cannot unequip.";
							else
								r = "Press Enter to unequip.";
						}
						else
							r = "Press Enter to equip.";
					}
					else if (i.HasToken("quest"))
						r = "This is a quest key item.";
					else
						r = "Press Enter to try and use.";

					howTo.Text = (' ' + r).PadRight(80);
					itemDesc.Text = d;
					//howTo.Draw();
					//itemDesc.Draw();
					UIManager.Draw();
				};
				itemList.Enter = (s, e) =>
				{
					TryUse(player.Character, inventoryTokens[itemList.Index], inventoryItems[itemList.Index]);
				};
				UIManager.Elements.Add(howTo);
				UIManager.Elements.Add(itemList);
				UIManager.Elements.Add(itemDesc);
				UIManager.Elements.Add(new UIButton("Drop", (s, e) => { TryDrop(player, inventoryTokens[itemList.Index], inventoryItems[itemList.Index]); }) { Left = 70, Top = 21, Width = 6, Height = 1 });
				UIManager.Highlight = itemList;
				itemList.Index = selection;

				UIManager.Draw();
			}

			if (keys[(int)Keys.Escape] || keys[(int)Keys.I])
			{
				NoxicoGame.ClearKeys();
				NoxicoGame.Immediate = true;
				NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
				NoxicoGame.HostForm.Noxico.CurrentBoard.Draw(true);
				NoxicoGame.Mode = UserMode.Walkabout;
				Subscreens.FirstDraw = true;
			}
			else if (keys[(int)Keys.D])
			{
				TryDrop(player, inventoryTokens[itemList.Index], inventoryItems[itemList.Index]);
			}
			else
				UIManager.CheckKeys();
		}
	}

}
