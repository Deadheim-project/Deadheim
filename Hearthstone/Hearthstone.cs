using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using Deadheim.Shared;
using HarmonyLib;
using ItemManager;
using ServerSync;
using UnityEngine;

namespace Hearthstone
{
	[BepInPlugin(PluginGUID, "Hearthstone", Version)]
	public class Hearthstone : BaseUnityPlugin
	{
		public const string PluginGUID = "Detalhes.Hearthstone";
		// 2.1.1: a pedra respeita a recusa do Deadheim (__runOriginal) e so gasta se teleportar;
		// ponto de retorno gravado com ponto decimal. MinimumRequiredVersion = Version: todo
		// cliente precisa da correcao, senao a pedra continua escapando de luta.
		public const string Version = "2.1.1";

		private Harmony harmony = new Harmony(PluginGUID);

		public static ConfigEntry<bool> allowTeleportWithoutRestriction;

		private ConfigSync configSync = new ConfigSync(PluginGUID)
		{
			DisplayName = "Hearthstone",
			CurrentVersion = Version,
			MinimumRequiredVersion = Version
		};

		private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
		{
			ConfigEntry<T> configEntry = base.Config.Bind<T>(group, name, value, description);
			SyncedConfigEntry<T> syncedConfigEntry = this.configSync.AddConfigEntry<T>(configEntry);
			syncedConfigEntry.SynchronizedConfig = synchronizedSetting;
			return configEntry;
		}

		private ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true)
		{
			return this.config<T>(group, name, value, new ConfigDescription(description, null, Array.Empty<object>()), synchronizedSetting);
		}

		private void Awake()
		{
			Hearthstone.allowTeleportWithoutRestriction = this.config<bool>("General", "allowTeleportWithoutRestriction", false, "Allow teleport without restriction", true);
			Item item = new Item("hearthstone", "Hearthstone", "assets");
			item.RequiredItems.Add("Crystal", 3);
			item.RequiredItems.Add("Coins", 30);
			item.RequiredItems.Add("BoneFragments", 20);
			ItemDrop.ItemData.SharedData shared = item.Prefab.GetComponent<ItemDrop>().m_itemData.m_shared;
			shared.m_name = "Hearthstone";
			shared.m_description = "Brings you back home.";
			shared.m_itemType = ItemDrop.ItemData.ItemType.Consumable;
			shared.m_consumeStatusEffect = null;

			item.Crafting.Add(CraftingTable.Workbench, 1);

			this.harmony.PatchAll();
			// Salvar o cfg com o servidor ligado ja vale: recarrega e o ServerSync repassa.
			Deadheim.Shared.ConfigWatcher.Watch(base.Config, "Hearthstone");
		}

		private void Update()
		{
			Player localPlayer = Player.m_localPlayer;

			// 1. Segurança: Ignora se o jogador for nulo, estiver morto, ou com chat/menus abertos.
			if (localPlayer == null || localPlayer.IsDead() || Chat.instance?.HasFocus() == true || Console.IsVisible() || Menu.IsVisible())
				return;

			// 2. Correção do Hover: Usando o método oficial da API
			GameObject hoverObject = localPlayer.GetHoverObject();
			if (hoverObject == null)
				return;

			// 3. Busca o componente Bed diretamente
			Bed bed = hoverObject.GetComponentInParent<Bed>();

			// Se não for uma cama que estamos olhando, para por aqui e evita o erro!
			if (bed == null)
				return;

			ZNetView nview = bed.GetComponent<ZNetView>();

			// Verifica se o objeto de rede existe e é válido
			if (nview != null && nview.IsValid())
			{
				// Lê o ID do dono diretamente do ZDO da cama
				long ownerId = nview.GetZDO().GetLong(ZDOVars.s_owner, 0L);

				if (ownerId == localPlayer.GetPlayerID())
				{
					if (Input.GetKeyDown(KeyCode.P))
					{
						SetHearthStonePosition();
						localPlayer.Message(MessageHud.MessageType.Center, "Here is your new Hearthstone spawn", 0, null);
					}
				}
			}
		}

		public static Vector3 GetHearthStonePosition()
		{
			// Verifica se o dicionário customData tem a chave antes de tentar ler
			Player player = Player.m_localPlayer;
			if (player == null
				|| !player.m_customData.TryGetValue("positionX", out string rawX)
				|| !player.m_customData.TryGetValue("positionY", out string rawY)
				|| !player.m_customData.TryGetValue("positionZ", out string rawZ))
			{
				return Vector3.zero;
			}

			// Ponto ou virgula: o formato antigo gravava com a cultura do PC, e o mesmo personagem
			// (ServerCharacters) aberto num Windows de outro idioma lia "123,45" como 12345.
			if (!Numeros.TryLer(rawX, out float x) || !Numeros.TryLer(rawY, out float y) || !Numeros.TryLer(rawZ, out float z))
			{
				Debug.LogWarning($"[Hearthstone] Ponto de retorno ilegivel ({rawX}; {rawY}; {rawZ}): defina de novo na cama.");
				return Vector3.zero;
			}
			return new Vector3(x, y, z);
		}

		public static void SetHearthStonePosition()
		{
			if (Player.m_localPlayer == null) return;

			Vector3 position = Player.m_localPlayer.transform.position;

			// Grava diretamente no m_customData (cria a chave se não existir, atualiza se existir)
			// Sempre com ponto (InvariantCulture): o mesmo valor vale em qualquer idioma do Windows.
			Player.m_localPlayer.m_customData["positionX"] = Numeros.Escrever(position.x);
			Player.m_localPlayer.m_customData["positionY"] = Numeros.Escrever(position.y);
			Player.m_localPlayer.m_customData["positionZ"] = Numeros.Escrever(position.z);
		}
	}
}
