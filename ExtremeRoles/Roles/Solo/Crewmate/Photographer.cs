﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using AmongUs.GameOptions;

using ExtremeRoles.Helper;
using ExtremeRoles.Module;
using ExtremeRoles.Module.CustomOption;
using ExtremeRoles.Resources;
using ExtremeRoles.Roles.API;
using ExtremeRoles.Roles.API.Interface;
using ExtremeRoles.Performance;
using ExtremeRoles.Performance.Il2Cpp;

namespace ExtremeRoles.Roles.Solo.Crewmate;

#nullable enable

public sealed class Photographer :
    SingleRoleBase,
    IRoleAbility,
    IRoleAwake<RoleTypes>,
    IRoleReportHook
{
    public readonly struct PlayerPosInfo
    {
		public readonly string PlayerName;
		public readonly SystemTypes? Room;

		public PlayerPosInfo(
            GameData.PlayerInfo player)
        {
            this.PlayerName = player.PlayerName;
			Player.TryGetPlayerRoom(player.Object, out var room);
			this.Room = room;
		}
    }

    public readonly struct Photo
    {
		private readonly List<PlayerPosInfo> player;
		private readonly DateTime takeTime;

		private static readonly string[] randomStr = new string[]
        {
            "NoName",
            "NewFile",
            "NewPhoto",
            "sudo",
            "HelloWorld",
            "AmongUs",
            "yukieiji",
            "ExtremeRoles",
            "ExtremeSkins",
            "ExtremeHat",
            "ExtremeVisor",
            "ExtremeNamePlate",
            "ExR", "ExS","ExV",
            "ExH", "ExN"
        };

        public Photo(float range)
        {
            this.takeTime = DateTime.UtcNow;
            this.player = new List<PlayerPosInfo>();

            Vector3 photoCenter = CachedPlayerControl.LocalPlayer.PlayerControl.transform.position;

            foreach (var player in GameData.Instance.AllPlayers.GetFastEnumerator())
            {
                if (player == null ||
                    player.IsDead ||
                    player.Disconnected ||
                    player.Object == null) { continue; }

                Vector3 position = player.Object.transform.position;
                if (range >= Vector2.Distance(photoCenter, position))
                {
                    this.player.Add(new PlayerPosInfo(player));
                }

            }
        }

        public string ToString(bool isUpgrade)
        {
            StringBuilder photoInfoBuilder = new StringBuilder();
            photoInfoBuilder.AppendLine(
                $"{Translation.GetString("takePhotoTime")} : {this.takeTime}");
            photoInfoBuilder.AppendLine(
                $"{Translation.GetString("photoName")} : {GetRandomPhotoName()}");
            photoInfoBuilder.AppendLine("");
            if (this.player.Count <= 1)
            {
                photoInfoBuilder.AppendLine(
                    Translation.GetString("onlyMeOnPhoto"));
            }
            else
            {
                foreach (PlayerPosInfo playerInfo in this.player)
                {

                    string roomInfo = string.Empty;

                    if (isUpgrade && playerInfo.Room != null)
                    {
                        roomInfo =
                            FastDestroyableSingleton<TranslationController>.Instance.GetString(
                                playerInfo.Room.Value);
                    }

                    string photoPlayerInfo =
                        roomInfo == string.Empty ?
                        $"{playerInfo.PlayerName}" :
                        $"{playerInfo.PlayerName}   {roomInfo}";

                    photoInfoBuilder.AppendLine(photoPlayerInfo);
                }
            }
            return photoInfoBuilder.ToString().Trim('\r', '\n');
        }

        public static string GetRandomPhotoName()
        {
            // 適当な役職名とかを写真名にする
            List<string> photoName = new List<string>();

            // 適当な陣営
            int maxTeamId = Enum.GetValues(typeof(ExtremeRoleType)).Cast<int>().Max();
            ExtremeRoleType intedTeamId = (ExtremeRoleType)RandomGenerator.Instance.Next(
                maxTeamId + 1);
            photoName.Add(Translation.GetString(intedTeamId.ToString()));


            // 適当な役職名
            int maxRoleId = Enum.GetValues(typeof(ExtremeRoleId)).Cast<int>().Max();
            ExtremeRoleId roleId = (ExtremeRoleId)RandomGenerator.Instance.Next(
                maxRoleId + 1);

            if (roleId != ExtremeRoleId.Null ||
                roleId != ExtremeRoleId.VanillaRole)
            {
                photoName.Add(Translation.GetString(roleId.ToString()));
            }
            else
            {
                int maxAmongUsRoleId = Enum.GetValues(typeof(RoleTypes)).Cast<int>().Max();
                RoleTypes amongUsRoleId = (RoleTypes)RandomGenerator.Instance.Next(
                    maxAmongUsRoleId + 1);

                photoName.Add(Translation.GetString(amongUsRoleId.ToString()));
            }

            photoName.Add(randomStr[RandomGenerator.Instance.Next(randomStr.Length)]);
            return string.Concat(photoName.OrderBy(
                item => RandomGenerator.Instance.Next()));
        }
    }

    private sealed class PhotoCamera
    {
        public bool IsUpgraded = false;

        private const string separateLine = "---------------------------------";
        private float range;
        private List<Photo> film = new List<Photo>();

        public PhotoCamera(float range)
        {
            this.range = range;
            this.film.Clear();
        }
        public void Reset()
        {
            this.film.Clear();
        }

        public void TakePhoto()
        {
            this.film.Add(new Photo(this.range));
        }
        public override string ToString()
        {
            if (this.film.Count == 0) { return string.Empty; }

            StringBuilder builder = new StringBuilder();

            foreach (Photo photo in this.film)
            {
                builder.AppendLine(separateLine);
                builder.AppendLine(
                    photo.ToString(this.IsUpgraded));
                builder.AppendLine(separateLine);
            }

            return builder.ToString().Trim('\r', '\n');
        }
    }

    public enum PhotographerOption
    {
        AwakeTaskGage,
        UpgradePhotoTaskGage,
        EnableAllSendChat,
        UpgradeAllSendChatTaskGage,
        PhotoRange,
    }
    public bool IsAwake
    {
        get
        {
            return GameSystem.IsLobby || this.awakeRole;
        }
    }

    public RoleTypes NoneAwakeRole => RoleTypes.Crewmate;
    private bool awakeRole;
    private float awakeTaskGage;
    private bool awakeHasOtherVision;

    private float upgradePhotoTaskGage;
    private bool enableAllSend;
    private float upgradeAllSendChatTaskGage;
    private bool isUpgradeChat;

    private SpriteRenderer? flash;
    public const float FlashTime = 1.0f;

#pragma warning disable CS8618
	public ExtremeAbilityButton Button { get; set; }
	private PhotoCamera photoCreater;
	public Photographer() : base(
        ExtremeRoleId.Photographer,
        ExtremeRoleType.Crewmate,
        ExtremeRoleId.Photographer.ToString(),
        ColorPalette.PhotographerVerdeSiena,
        false, true, false, false)
    { }
#pragma warning restore CS8618

	public void CreateAbility()
    {
        this.CreateAbilityCountButton(
            "takePhoto",
            Loader.CreateSpriteFromResources(
                Path.PhotographerPhotoCamera));
        this.Button.SetLabelToCrewmate();
    }

    public bool UseAbility()
    {
        var hudManager = FastDestroyableSingleton<HudManager>.Instance;

        if (this.flash == null)
        {
            this.flash = UnityEngine.Object.Instantiate(
                 hudManager.FullScreen,
                 hudManager.transform);
            this.flash.transform.localPosition = new Vector3(0f, 0f, 20f);
            this.flash.gameObject.SetActive(true);
        }

        this.flash.enabled = true;

        this.photoCreater.TakePhoto();

        hudManager.StartCoroutine(
            Effects.Lerp(FlashTime, new Action<float>((p) =>
            {
                if (this.flash == null) { return; }
                if (p < 0.25f)
                {
                    this.flash.color = new Color(
                        255f, 255f, 255f, Mathf.Clamp01(p * 5 * 0.75f));

                }
                else if (p >= 0.5f)
                {
                    this.flash.color = new Color(
                        255f, 255f, 255f, Mathf.Clamp01((1 - p) * 5 * 0.75f));
                }
                if (p == FlashTime)
                {
                    this.flash.enabled = false;
                }
            }))
        );
        return true;
    }

    public bool IsAbilityUse()
        => this.IsAwake && this.IsCommonUse();

    public string GetFakeOptionString() => "";

    public void HookReportButton(
        PlayerControl rolePlayer, GameData.PlayerInfo reporter)
    {
        sendPhotoInfo();
    }

    public void HookBodyReport(
        PlayerControl rolePlayer,
        GameData.PlayerInfo reporter,
        GameData.PlayerInfo reportBody)
    {
        sendPhotoInfo();
    }

    public void ResetOnMeetingStart()
    {
        if (this.flash != null)
        {
            this.flash.enabled = false;
        }
    }

    public void ResetOnMeetingEnd(GameData.PlayerInfo? exiledPlayer = null)
    {
        this.photoCreater.Reset();
    }

    public void Update(PlayerControl rolePlayer)
    {
        float taskGage = Player.GetPlayerTaskGage(rolePlayer);

        if (!this.awakeRole)
        {
            if (taskGage >= this.awakeTaskGage && !this.awakeRole)
            {
                this.awakeRole = true;
                this.HasOtherVision = this.awakeHasOtherVision;
                this.Button.SetButtonShow(true);
            }
            else
            {
                this.Button.SetButtonShow(false);
            }
        }

        if (taskGage >= this.upgradePhotoTaskGage &&
            !this.photoCreater.IsUpgraded)
        {
            this.photoCreater.IsUpgraded = true;
        }
        if (this.enableAllSend &&
            taskGage >= this.upgradeAllSendChatTaskGage &&
            !this.isUpgradeChat)
        {
            this.isUpgradeChat = true;
        }
    }

    public override string GetColoredRoleName(bool isTruthColor = false)
    {
        if (isTruthColor || IsAwake)
        {
            return base.GetColoredRoleName();
        }
        else
        {
            return Design.ColoedString(
                Palette.White, Translation.GetString(RoleTypes.Crewmate.ToString()));
        }
    }
    public override string GetFullDescription()
    {
        if (IsAwake)
        {
            return Translation.GetString(
                $"{this.Id}FullDescription");
        }
        else
        {
            return Translation.GetString(
                $"{RoleTypes.Crewmate}FullDescription");
        }
    }

    public override string GetImportantText(bool isContainFakeTask = true)
    {
        if (IsAwake)
        {
            return base.GetImportantText(isContainFakeTask);

        }
        else
        {
            return Design.ColoedString(
                Palette.White,
                $"{this.GetColoredRoleName()}: {Translation.GetString("crewImportantText")}");
        }
    }

    public override string GetIntroDescription()
    {
        if (IsAwake)
        {
            return base.GetIntroDescription();
        }
        else
        {
            return Design.ColoedString(
                Palette.CrewmateBlue,
                CachedPlayerControl.LocalPlayer.Data.Role.Blurb);
        }
    }

    public override Color GetNameColor(bool isTruthColor = false)
    {
        if (isTruthColor || IsAwake)
        {
            return base.GetNameColor(isTruthColor);
        }
        else
        {
            return Palette.White;
        }
    }

    protected override void CreateSpecificOption(
        IOptionInfo parentOps)
    {
        CreateIntOption(
            PhotographerOption.AwakeTaskGage,
            30, 0, 100, 10,
            parentOps,
            format: OptionUnit.Percentage);

        CreateIntOption(
            PhotographerOption.UpgradePhotoTaskGage,
            60, 0, 100, 10,
            parentOps,
            format: OptionUnit.Percentage);

        var chatUpgradeOpt = CreateBoolOption(
            PhotographerOption.EnableAllSendChat,
            false, parentOps, isHidden: true);

        CreateIntOption(
            PhotographerOption.UpgradeAllSendChatTaskGage,
            80, 0, 100, 10,
            chatUpgradeOpt,
            format: OptionUnit.Percentage,
			isHidden: true);

        CreateFloatOption(
            PhotographerOption.PhotoRange,
            10.0f, 2.5f, 50f, 0.5f,
            parentOps);

        this.CreateAbilityCountOption(
            parentOps, 5, 10);
    }

    protected override void RoleSpecificInit()
    {
        var allOpt = OptionManager.Instance;

        this.awakeTaskGage = allOpt.GetValue<int>(
            GetRoleOptionId(PhotographerOption.AwakeTaskGage)) / 100.0f;
        this.upgradePhotoTaskGage = allOpt.GetValue<int>(
            GetRoleOptionId(PhotographerOption.UpgradePhotoTaskGage)) / 100.0f;
		this.enableAllSend = false;
			/*
			allOpt.GetValue<bool>(
				GetRoleOptionId(PhotographerOption.EnableAllSendChat));
			*/
        this.upgradeAllSendChatTaskGage = allOpt.GetValue<int>(
            GetRoleOptionId(PhotographerOption.UpgradeAllSendChatTaskGage)) / 100.0f;

        this.awakeHasOtherVision = this.HasOtherVision;

        if (this.awakeTaskGage <= 0.0f)
        {
            this.awakeRole = true;
            this.HasOtherVision = this.awakeHasOtherVision;
        }
        else
        {
            this.awakeRole = false;
            this.HasOtherVision = false;
        }

        this.isUpgradeChat = this.enableAllSend &&
            this.upgradeAllSendChatTaskGage <= 0.0f;

        this.photoCreater = new PhotoCamera(
            allOpt.GetValue<float>(
                GetRoleOptionId(PhotographerOption.PhotoRange)));

        this.photoCreater.IsUpgraded = this.upgradePhotoTaskGage <= 0.0f;

        this.RoleAbilityInit();

    }

    private void sendPhotoInfo()
    {
        if (!this.IsAwake) { return; }

        string photoInfo = this.photoCreater.ToString();

        HudManager hud = FastDestroyableSingleton<HudManager>.Instance;

        if (photoInfo == string.Empty ||
            !AmongUsClient.Instance.AmClient ||
            hud == null) { return; }

        string chatText = string.Format(
            Translation.GetString("photoChat"),
            photoInfo);

        MeetingReporter.Instance.AddMeetingChatReport(
            chatText, this.enableAllSend && this.isUpgradeChat);
    }
}
