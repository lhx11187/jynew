/*
 * 金庸群侠传3D重制版
 * https://github.com/jynew/jynew
 *
 * 这是本开源项目文件头，所有代码均使用MIT协议。
 * 但游戏内资源和第三方插件、dll等请仔细阅读LICENSE相关授权协议文档。
 *
 * 金庸老先生千古！
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Jyx2;
using SkillEffect;
using UniRx;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;
using Cysharp.Threading.Tasks;
using Jyx2.Middleware;
using Jyx2Configs;

public class MapRole : Jyx2AnimationBattleRole
{
    //模型资源
    private ModelAsset modelAsset
    {
        get
        {
            return DataInstance.Data.Model;
        }
    }

    #region 角色基本信息

    /// <summary>
    /// 数据实例，人物各项信息
    /// </summary>
    public RoleInstance DataInstance
    {
        get
        {
            if (_dataInstance == null) this.CreateRoleInstance(m_RoleKey);
            return _dataInstance;
        }
        set
        {
            _dataInstance = value;
        }
    }
    private RoleInstance _dataInstance;
    
    /// <summary>
    /// 角色模型
    /// </summary>
    private GameObject ModelView;
    
    /// <summary>
    /// 人物Key索引
    /// </summary>
    public int m_RoleKey;
    
    /// <summary>
    /// 当前武器
    /// </summary>
    GameObject m_CurrentWeapon = null;

    /// <summary>
    /// 是否关键角色
    /// 如果是关键角色则死亡后只会眩晕？(如果这样的话眩晕还存在逻辑问题，目前角色死亡会立刻判定死亡)
    /// </summary>
    public bool m_IsKeyRole = false; //主角+队友+面板打勾的

    /// <summary>
    /// 是否等待活动
    /// TODO:没看懂什么意思
    /// </summary>
    [HideInInspector]
    public bool m_IsWaitingForActive = true; //GameObject Active时触发的标记

    /// <summary>
    /// 是否在战斗中
    /// </summary>
    [HideInInspector]
    public bool IsInBattle = false;
    
    /// <summary>
    /// 队友寻路组件
    /// </summary>
    private NavMeshAgent _navMeshAgent;

    #endregion
    
    #region 动画脚本设置
    
    /// <summary>
    /// 是否懒加载
    /// TODO:是否须臾奥原本值设置为false?因为按照逻辑来说都是true。
    /// </summary>
    private bool _lazyInitBattleAnimator = true;
    
    /// <summary>
    /// 懒加载函数
    /// </summary>
    public void LazyInitAnimator()
    {
        _lazyInitBattleAnimator = true;
    }
    
    /// <summary>
    /// 获取角色的Animator组件
    /// TODO:懒加载我没看懂，技术不到家，所以没动
    /// </summary>
    /// <returns>物体身上的Animator组件</returns>
    public override Animator GetAnimator()
    {
        if (_animator == null && transform.childCount == 0)
            return null;

        if (_animator == null)
        {
            for (var index = 0; index < transform.childCount; index++)
            {
                _animator = transform.GetChild(index).GetComponentInChildren<Animator>();
                if (_animator != null)
                {
                    break;
                }
            }
        }

        if (_lazyInitBattleAnimator)
        {
            if (_navMeshAgent != null) _navMeshAgent.enabled = false;

            if (_animator != null)
            {
                _animator.SetBool("IsInBattle", true);
                _animator.applyRootMotion = false; //不接受动作变化位置
                _animator.SetFloat("speed", 0);
            }

            _lazyInitBattleAnimator = false;
        }
        
        return _animator;
    }

    private Animator _animator;

    #endregion
    
    #region 角色聊天/NPC闲聊
    
    /// <summary>
    /// NPC说的话
    /// </summary>
    public string[] m_NpcWords;
    
    /// <summary>
    /// 角色说话
    /// </summary>
    /// <param name="word">角色发言内容</param>
    /// <param name="time">显示持续时间（默认5s）</param>
    public void Say(string word, float time = 5f)
    {
        var hudRoot = StoryEngine.Instance.HUDRoot;

        HUDTextInfo info = new HUDTextInfo(transform, word)
        {
            Size = 12,
            Color = Color.white,
            VerticalPositionOffset = 0.5f,
            ExtraDelayTime = time
        };
        hudRoot.NewText(info);
    }

    /// <summary>
    /// NPC闲聊
    /// </summary>
    public void DoNpcChat()
    {
        if (m_NpcWords != null && m_NpcWords.Length > 0)
        {
            string randomWord = m_NpcWords[UnityEngine.Random.Range(0, m_NpcWords.Length)];
            HUDTextInfo info4 = new HUDTextInfo(transform, randomWord)
            {
                Color = Color.white,
                Size = 15,
                VerticalPositionOffset = 1f,
                VerticalFactorScale = UnityEngine.Random.Range(1.2f, 3),
                Side = bl_Guidance.Right,
                FadeSpeed = 500,
                ExtraDelayTime = 5,
                AnimationType = bl_HUDText.TextAnimationType.HorizontalSmall
            };
            //Send the information
            var hudRoot = StoryEngine.Instance.HUDRoot;
            hudRoot.NewText(info4);
        }

        transform.LookAt(GameRuntimeData.Instance.Player.View.transform);
    }

    #endregion

    #region 战斗伤害
    
    //用于显示掉血效果
    private int _showDamage;
    
    /// <summary>
    /// 设置伤害数值大小
    /// TODO:原本有一个Hp选项，不知道有什么用，于是给删除了，目前看来似乎没用
    /// </summary>
    /// <param name="damage">受到的伤害</param>
    public void SetDamage(int damage)
    {
        _showDamage = damage;
    }
    
    /// <summary>
    /// 展示掉血信息
    /// </summary>
    public override void ShowDamage()
    {
        //JYX2逻辑，不存在MISS
        if (_showDamage <= 0)
            return;

        if (StoryEngine.Instance == null) return;

        var hudRoot = StoryEngine.Instance.HUDRoot;
        string damageText = "";
        if (_showDamage > 0)
            damageText = $"-{Math.Max(_showDamage, 1)}";
        else
            damageText = "MISS";
        //显示后重置
        _showDamage = 0;
        HUDTextInfo info = new HUDTextInfo(transform, $"{damageText}")
        {
            Color = Color.white,
            //Size = Random.Range(1, 12),
            Speed = Random.Range(0.2f, 1),
            VerticalAceleration = Random.Range(-2, 2f),
            VerticalPositionOffset = Random.Range(0, 0.8f),
            VerticalFactorScale = Random.Range(1.2f, 10),
            Side = (Random.Range(0, 2) == 1) ? bl_Guidance.LeftDown : bl_Guidance.RightDown,
            ExtraDelayTime = 0.2f,
            AnimationType = bl_HUDText.TextAnimationType.HorizontalSmall,
            FadeSpeed = 200,
            ExtraFloatSpeed = -11
        };

        info.TextPrefab = Jyx2ResourceHelper.GetCachedPrefab("AttackInfoText");
        hudRoot.NewText(info);
    }

    #endregion

    #region 血条设定
    
    /// <summary>
    /// 是否为脏数据需要刷新
    /// </summary>
    public bool HPBarIsDirty { private set; get; } = false;
    
    /// <summary>
    /// 血条标记为需要刷新
    /// </summary>
    public override void MarkHpBarIsDirty()
    {
        HPBarIsDirty = true;
    }

    /// <summary>
    /// 取消刷新血条标记
    /// </summary>
    public override void UnmarkHpBarIsDirty()
    {
        HPBarIsDirty = false;
    }

    #endregion

    #region 战场/攻击信息显示

    /// <summary>
    /// 展示战场信息
    /// </summary>
    /// <param name="mainText">显示文字</param>
    /// <param name="textColor">文字颜色</param>
    public void ShowBattleText(string mainText,Color textColor) 
    {
        if (StoryEngine.Instance == null) return;

        var hudRoot = StoryEngine.Instance.HUDRoot;
        HUDTextInfo info = new HUDTextInfo(transform, $"{mainText}")
        {
            Color = Color.white,
            Speed = Random.Range(0.2f, 1),
            VerticalAceleration = Random.Range(-2, 2f),
            VerticalPositionOffset = Random.Range(0, 0.8f),
            VerticalFactorScale = Random.Range(1.2f, 10),
            Side = (Random.Range(0, 2) == 1) ? bl_Guidance.LeftDown : bl_Guidance.RightDown,
            ExtraDelayTime = 0.2f,
            AnimationType = bl_HUDText.TextAnimationType.HorizontalSmall,
            FadeSpeed = 200,
            ExtraFloatSpeed = -11
        };
        info.Color = textColor;

        info.TextPrefab = Jyx2ResourceHelper.GetCachedPrefab("AttackInfoText");
        hudRoot.NewText(info);
    }

    /// <summary>
    /// 展示攻击信息
    /// </summary>
    /// <param name="content">显示内容</param>
    public void ShowAttackInfo(string content)
    {
        var hudRoot = StoryEngine.Instance.HUDRoot;

        HUDTextInfo info = new HUDTextInfo(transform, content)
        {
            Color = Color.white,
            //Size = Random.Range(1, 12),
            Speed = Random.Range(0.2f, 1),
            VerticalAceleration = Random.Range(-2, 2f),
            VerticalPositionOffset = Random.Range(0, 0.8f),
            VerticalFactorScale = Random.Range(1.2f, 10),
            Side = (Random.Range(0, 2) == 1) ? bl_Guidance.LeftDown : bl_Guidance.RightDown,
            ExtraDelayTime = 0.2f,
            AnimationType = bl_HUDText.TextAnimationType.HorizontalSmall,
            FadeSpeed = 200,
            ExtraFloatSpeed = -11
        };

        info.TextPrefab = Jyx2ResourceHelper.GetCachedPrefab("AttackInfoText");
        hudRoot.NewText(info);
    }

    #endregion

    #region 模型动作相关
    
    /// <summary>
    /// 命令行播放动画
    /// TODO:这里应该是配合lua的，我先自己写一种方式，后续方式再看看吧
    /// 示例:
    /// Action:Idle,ChangeWeapon:1,SwitchSkillTo,1
    /// </summary>
    /// <param name="cmd">命令行</param>
    public void PlayAnimationCmd(string cmd)
    {
        foreach (var c in cmd.Split(','))
        {
            string para = c.Split(':')[0];
            string value = c.Split(':')[1];
            //正常动作播放
            if (para == "Action")
            {
                switch (value)
                {
                    case "Idle":
                        Idle();
                        return;
                    case "Run":
                        Run();
                        return;
                    case "Attack":
                        Attack();
                        return;
                }
            }
            //替换武器
            else if (para == "ChangeWeapon")
            {
                int weaponType = int.Parse(value);
                if (Enum.IsDefined(typeof(ModelAsset.WeaponPartType), weaponType))
                {
                    ChangeWeapon((ModelAsset.WeaponPartType)Enum.ToObject(typeof(ModelAsset.WeaponPartType), weaponType));
                }
            }
            //切换技能
            else if (para == "SwitchSkillTo")
            {
                var skill = new SkillInstance(int.Parse(value));
                SwitchSkillTo(skill);
            }
        }
    }

    /// <summary>
    /// 切换武学，载入动作后进入站立状态
    /// </summary>
    /// <param name="skill">技能实例</param>
    public void SwitchSkillTo(SkillInstance skill)
    {
        var display = skill.GetDisplay();
        //切换对应武器
        var weaponCode = display.weaponCode;
        if (weaponCode >= 0)
        {
            ChangeWeapon(weaponCode);
        }

        this.CurDisplay = display;
        
        //载入动作
        ChangeAnimationController(display.GetAnimationController(), Idle);
    }

    /// <summary>
    /// 更换模型武器
    /// </summary>
    /// <param name="weaponCode">武器类别</param>
    void ChangeWeapon(ModelAsset.WeaponPartType weaponCode)
    {
        DOMountWeapon(weaponCode);
    }

    /// <summary>
    /// 挂载武器
    /// </summary>
    /// <param name="weaponCode">武器类型</param>
    void DOMountWeapon(ModelAsset.WeaponPartType weaponCode)
    {
        //if(!IsInBattle) return;
        UnMountCurrentWeapon();

        if (weaponCode == 0)
            return;
        if(modelAsset == null) return;
        
        var weapon = modelAsset.GetWeaponPart(weaponCode);
        if(weapon == null) return;
        if(weapon.m_PartView == null) return;

        m_CurrentWeapon = Instantiate(weapon.m_PartView);
        var parent = UnityTools.DeepFindChild(transform, weapon.m_BindBone);
        if(parent != null)
        {
            m_CurrentWeapon.transform.SetParent(parent.transform);
            m_CurrentWeapon.transform.localScale = weapon.m_OffsetScale;
            m_CurrentWeapon.transform.localPosition = weapon.m_OffsetPosition;
            m_CurrentWeapon.transform.localRotation = Quaternion.Euler(weapon.m_OffsetRotation);
        }
        else
        {
            Debug.LogError("武器挂载到了不存在的节点：" + weapon.m_BindBone);
        }
    }

    /// <summary>
    /// 卸载武器
    /// </summary>
    void UnMountCurrentWeapon()
    {
        if(m_CurrentWeapon == null) return;
        
        m_CurrentWeapon.transform.localScale = Vector3.zero;
    }

    /// <summary>
    /// 重定向角色Controller
    /// TODO:目前不知道有什么用，到时候再看看
    /// </summary>
    /// <param name="controller"></param>
    /// <param name="callback"></param>
    void ChangeAnimationController(RuntimeAnimatorController controller, Action callback)
    {
        if (controller == null)
            return;

        var anim = GetAnimator();
        if (anim == null)
            return;

        //无论如何重新绑定Controller
        anim.runtimeAnimatorController = controller;
        callback();
    }


    /// <summary>
    /// 使人物朝向一个方向，根据战场格子计算
    /// </summary>
    /// <param name="pos">朝向的格子位置坐标</param>
    public void LookAtBattleBlock(BattleBlockData pos)
    {
        //平视，所以y轴和自身一致
        transform.LookAt(new Vector3(pos.WorldPos.x, transform.position.y, pos.WorldPos.z));
    }

    /// <summary>
    /// 使人物朝向一个方向，根据世界坐标计算
    /// </summary>
    /// <param name="position">世界位置坐标</param>
    public void LookAtWorldPosInBattle(Vector3 position)
    {
        //平视，所以y轴和自身一致
        transform.LookAt(new Vector3(position.x, transform.position.y, position.z));
    }

    /// <summary>
    /// 设置角色位置
    /// </summary>
    /// <param name="pos">世界位置坐标</param>
    public void SetPosition(Vector3 pos) 
    {
        transform.position = pos;
    }

    #endregion

    #region 人物模型状态及其动作

    /// <summary>
    /// 人物健康状态
    /// TODO:目前未知作用，应该是某些特定药品对于某些作用吧？
    /// </summary>
    private MapRoleHealth m_Health = MapRoleHealth.Normal;
    
    /// <summary>
    /// 人物死亡
    /// </summary>
    /// <param name="deathCode">TODO:不知道什么用，应该是判断死亡形态吧？总之还没开发完。</param>
    public void ShowDeath(int deathCode = -1)
    {
        var globalConfig = GlobalAssetConfig.Instance;

        if (this._animator == null) return;

        //人型骨骼，播放死亡动作
        if (this._animator.runtimeAnimatorController == globalConfig.defaultAnimatorController)
        {
            var clip = Jyx2.Middleware.Tools.GetRandomElement(globalConfig.defaultDieClips);
            PlayAnimation(clip, () => { Destroy(gameObject); });
        }
        else
        {
            Destroy(gameObject);  
        }
        
        m_Health = MapRoleHealth.Death;
    }

    /// <summary>
    /// 人物眩晕
    /// TODO:此部分配合全局的动作进行了重写，但是寻路操作在技能展示场景会有BUG，因为技能展示场景没有寻路脚本，会报错。
    /// </summary>
    public void ShowStun()
    {
        //重写眩晕
        AnimationClip clip = null;
        clip = (CurDisplay == null)?GlobalAssetConfig.Instance.defaultStunClip:CurDisplay.LoadAnimation(Jyx2SkillDisplayAsset.Jyx2RoleAnimationType.Stun);
        PlayAnimation(clip);
        
        if(_navMeshAgent)//TODO:这里之所以加这一行是因为如果在Skill测试场景中没有寻路，建议后期去掉
            _navMeshAgent.enabled = false;
        if (m_Health == MapRoleHealth.Normal) m_Health = MapRoleHealth.Stun;
    }

    /// <summary>
    /// 解除人物眩晕状态
    /// TODO:此部分配合全局的动作进行了重写，但是寻路操作在技能展示场景会有BUG，因为技能展示场景没有寻路脚本，会报错。
    /// </summary>
    /// <param name="isInBattle">是否在战斗中</param>
    public void StopStun(bool isInBattle)
    {
        if(_navMeshAgent)//TODO:这里之所以加这一行是因为如果在Skill测试场景中没有寻路，建议后期去掉
            _navMeshAgent.enabled = !isInBattle;
        
        //重写解除眩晕并且简化逻辑
        AnimationClip clip = null;
        clip = CurDisplay == null?GlobalAssetConfig.Instance.defaultIdleClip:CurDisplay.LoadAnimation(Jyx2SkillDisplayAsset.Jyx2RoleAnimationType.Idle);
        PlayAnimation(clip);
        
        if (m_Health != MapRoleHealth.Death) m_Health = MapRoleHealth.Stun;
    }
    
    /// <summary>
    /// 没死亡则站立
    /// </summary>
    public override void DeadOrIdle()
    {
        if (DataInstance.IsDead())
        {
            ShowDeath();
        }
        else
        {
            Idle();
        }
    }

    /// <summary>
    /// 刷新模型（更换模型）
    /// </summary>
    public async UniTask RefreshModel()
    {
        if (DataInstance == null) return;
        
        if (Application.isPlaying)
        {
            //销毁所有的孩子
            HSUnityTools.DestroyChildren(transform);
        }

        if (modelAsset == null) return;
        
        ModelView = Instantiate(modelAsset.m_View, gameObject.transform, false);
        ModelView.transform.localPosition = Vector3.zero;

        //如果在战斗中
        if (IsInBattle)
        {
            //直接用第一个武功的姿势
            DataInstance.SwitchAnimationToSkill(DataInstance.Wugongs[0]);
        }
        else
        {
            //默认是休息状态
            Idle();
        }
    }
    #endregion

    #region 脚本初始化

    private void Awake()
    {
        //设置寻路组件
        //TODO:其中部分函数用到_nav但可能为空，已用TODO标记。
        _navMeshAgent = GetComponent<NavMeshAgent>();
    }

    private async void Start()
    {
        await BeforeSceneLoad.loadFinishTask;
        
        //场景没有LevelMaster
        if (LevelMaster.Instance == null && DataInstance == null) this.CreateRoleInstance(m_RoleKey);
        
        //脚步声
        //Observable.EveryFixedUpdate()
        //    .Where(_ => _navMeshAgent != null)
        //    .Subscribe(xs =>
        //    {
        //        PlayFootStepSoundEffect(_distFromPlayer);
        //    });
        
        if (m_IsWaitingForActive)
        {
            m_IsWaitingForActive = false;
            await RefreshModel();
        }
    }

    #endregion

    #region 角色残影

    GhostShadow m_ghostShadow;

    public void BeginGhostShadow(Color color)
    {
        m_ghostShadow = GetComponent<GhostShadow>();
        if (m_ghostShadow == null)
            m_ghostShadow = gameObject.AddComponent<GhostShadow>();

        m_ghostShadow.m_Color = color;
        m_ghostShadow.m_bOpenGhost = true;
    }

    public void StopGhostShadow()
    {
        if (m_ghostShadow != null)
        {
            m_ghostShadow.enabled = false;
            GameObject.Destroy(m_ghostShadow);
            m_ghostShadow = null;
        }
    }
    #endregion
}

public static class MapRoleTools
{
    public static IEnumerable<MapRole> ToMapRoles(this List<RoleInstance> roles)
    {
        foreach (var role in roles)
        {
            yield return role.View;
        }
    }
}
