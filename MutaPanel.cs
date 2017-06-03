using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Noxico
{
	public partial class MutaPanel : Form
	{
		public MutaPanel()
		{
			// since we're not loading the game normally, I gotta do a lot of stuff manually
			InitializeComponent();
			Mix.Initialize("Noxico");
			//NoxicoGame.Initialize(this);

			// load biomes
			BiomeData.LoadBiomes();

			// bindings
			NoxicoGame.KeyBindings = new Dictionary<KeyBinding, Keys>();
			NoxicoGame.RawBindings = new Dictionary<KeyBinding, string>();
			var keyNames = Enum.GetNames(typeof(Keys)).Select(x => x.ToUpperInvariant());
			//Keep this array in synch with KeyBinding.
			var defaults = new[]
			{
				Keys.Left, Keys.Right, Keys.Up, Keys.Down,
				Keys.OemPeriod, Keys.Enter, Keys.OemQuotes, Keys.OemQuestion,
				Keys.Oemcomma, Keys.OemSemicolon, Keys.Enter, Keys.Escape,
				Keys.F1, Keys.F12, Keys.Tab,
				Keys.Up, Keys.Down
			};
			for (var i = 0; i < defaults.Length; i++)
			{
				var iniKey = ((KeyBinding)i).ToString().ToLowerInvariant();
				var iniValue = IniFile.GetValue("keymap", iniKey, Enum.GetName(typeof(Keys), defaults[i])).ToUpperInvariant();
				if (keyNames.Contains(iniValue))
				{
					NoxicoGame.KeyBindings[(KeyBinding)i] = (Keys)Enum.Parse(typeof(Keys), iniValue, true);
				}
				else
				{
					//Try unfriendly name
					iniValue = "OEM" + iniValue;
					if (keyNames.Contains(iniValue))
						NoxicoGame.KeyBindings[(KeyBinding)i] = (Keys)Enum.Parse(typeof(Keys), iniValue, true);
					else
					{
						//Give up, use default
						NoxicoGame.KeyBindings[(KeyBinding)i] = defaults[i];
						iniValue = defaults[i].ToString().ToUpperInvariant();
					}
				}
				NoxicoGame.RawBindings[(KeyBinding)i] = iniValue;
			}

			// load items
			var items = Mix.GetTokenTree("items.tml");
			NoxicoGame.KnownItems = new List<InventoryItem>();
			foreach (var item in items.Where(t => t.Name == "item" && !t.HasToken("disabled")))
				NoxicoGame.KnownItems.Add(InventoryItem.FromToken(item));

			// load bodyplans and hashes
			NoxicoGame.BodyplanHashes = new Dictionary<string, string>();
			Character.Bodyplans = Mix.GetTokenTree("bodyplans.tml");
			foreach (var bodyPlan in Character.Bodyplans.Where(t => t.Name == "bodyplan"))
			{
				var id = bodyPlan.Text;
				var plan = bodyPlan.Tokens;
				Toolkit.VerifyBodyplan(bodyPlan, id);
				if (bodyPlan.HasToken("beast"))
					continue;
				NoxicoGame.BodyplanHashes.Add(id, Toolkit.GetBodyComparisonHash(bodyPlan));
			}

			// fake player to keep thing happy
			//NoxicoGame.HostForm.Noxico.Player.Character = Character.Generate("human", Gender.Male, Gender.Male, Realms.Nox);

			//// populate menus
			//foreach (Token bp in Character.Bodyplans)
			//{
			//	// todo
			//}

			// pop source box
			var newchar = Character.Generate("human", Gender.Male, Gender.Male, Realms.Nox);
			var Value = new List<Token>();
			sourceBox.Text = newchar.DumpTokens(newchar.Tokens.ToList(), 0);

			// pop target box
			var targetPlan = "felin";
			var targetToken = Character.Bodyplans.FirstOrDefault(x => x.Name == "bodyplan" && x.Text == targetPlan);
			Value.Add(targetToken);
			targetBox.Text = targetToken.DumpTokens(Value, 0);

			// populate deltas box
			var deltas = newchar.GetMorphDeltas("felin", Gender.Invisible);
			deltasBox.Text = newchar.DumpTokens(deltas, 0);

			// pop results.box
			var feedback = newchar.Morph("felin");
			resultBox.Text = newchar.DumpTokens(newchar.Tokens.ToList(), 0);

			// pop feedback box
			messagesBox.Text = feedback;
		}

		private void generateCharacterHereToolStripMenuItem_Click(object sender, EventArgs e)
		{

		}
	}
}
