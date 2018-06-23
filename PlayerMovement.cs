using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class PlayerMovement : MonoBehaviour {

	public interface IPlayerSubmovement {
		void Init(PlayerMovement player);
		Vector2 Update();
	}

	public enum MoveMode {
		STANDARD, WALL, BASH, STOMP, PUSH
	}
	public MoveMode mode = MoveMode.STANDARD;
	MoveMode lastmode = MoveMode.STANDARD;

	public Vector2 vel, overrideVel;
	public bool drawDebug;
	public bool printStateChanges;

	public bool applyGravity;

	public bool isGrounded;
	public float groundAngleMax = 40f;
	private float groundNormal;
	public float frictionSpeedSqr = 0.4f;

	public bool isWall;
	public float wallAngleMax = 120f;
	private float wallNormal;

	public PlayerStandardMovement pmove;

	public PlayerJump pjump;

	public PlayerStandardDash pdash;

	public PlayerWall pwall;

	public PlayerBash pbash;

	private bool lookingRight;
	private float horz, vert;
	private float initGravityScale, initFixedDeltaTimescale;

	public BoxCollider2D groundCollider, wallCollider;
	public CapsuleCollider2D bashCollider;

	public ParticleSystem.Burst jumpBurst;

	private Rigidbody2D body;
	private SpriteRenderer sr;
	private ParticleSystem partsys;
	private TrailRenderer trail;

	void Awake() {
		body = GetComponent<Rigidbody2D>();
		sr = GetComponentInChildren<SpriteRenderer>();
		partsys = GetComponentInChildren<ParticleSystem>();
		trail = GetComponentInChildren<TrailRenderer>();

		initFixedDeltaTimescale = Time.fixedDeltaTime;
		initGravityScale = body.gravityScale;
		applyGravity = true;

		pmove.Init(this);
		pjump.Init(this);
		pdash.Init(this);
		pwall.Init(this);
		pbash.Init(this);
	}

	void Update() {
		if (drawDebug) {
			Debug.DrawRay(transform.position, Quaternion.Euler(0, 0, groundNormal) * Vector2.up, Color.white);
			Debug.DrawRay(transform.position, body.velocity, Color.cyan);
		}
		if (lastmode != mode) {
			if (printStateChanges) print("new mode " + mode);
			lastmode = mode;
		}

		// RESET
		if (Input.GetKeyDown(KeyCode.Escape)) {
			body.position = Vector2.zero;
			body.velocity = Vector2.zero;
		}

		UpdateAnimator();
	}


	void FixedUpdate() {
		horz = InputWrapper.GetHorz();
		vert = InputWrapper.GetVert();

		vel = body.velocity;
		overrideVel = Vector2.positiveInfinity;

		body.gravityScale = applyGravity ? initGravityScale : 0;

		UpdateContacts();
		pbash.UpdateBashCommon();

		var freeze = isGrounded && vel.sqrMagnitude < frictionSpeedSqr;
		if (freeze) {
			vel = Vector2.zero;
			//body.constraints = RigidbodyConstraints2D.FreezeAll;
		} else {
			body.constraints = RigidbodyConstraints2D.FreezeRotation;
		}

		if (!isGrounded) {
			groundNormal = 0;
		}
		if (!isWall) {
			wallNormal = 0;
		}

		switch (mode) {
			case MoveMode.STANDARD:

				applyGravity = true;
				trail.enabled = true;

				OverrideNonZero(ref vel, pmove.Update());

				vel.x += pjump.Update().x;

				pdash.Update();

				if (!isGrounded && isWall) {
					mode = MoveMode.WALL;
				}

				break;
			case MoveMode.WALL:

				OverrideNonZero(ref vel, pwall.Update());
				OverrideNonZero(ref vel, pmove.Update());

				if (isGrounded || !isWall) {
					mode = MoveMode.STANDARD;
				}

				break;
			case MoveMode.BASH:
				if (!pbash.bashTarget) {
					mode = MoveMode.STANDARD;
					break;
				}
				pbash.Update();

				break;
			case MoveMode.STOMP:

				break;
			case MoveMode.PUSH:

				break;
			default:
				mode = MoveMode.STANDARD;
				break;
		}

		OverrideNonInf(ref vel, overrideVel);
		body.velocity = vel;
	}

	#region Contacts

	void UpdateContacts() {
		if (Physics2D.OverlapBox(body.position + groundCollider.offset, groundCollider.size, 0, ~(1 << 8) /*player*/) == null) {
			isGrounded = false;
		}

		var otherCenter = wallCollider.offset;
		otherCenter.x *= -1f;
		if (Physics2D.OverlapBox(body.position + wallCollider.offset, wallCollider.size, 0, ~(1 << 8) /*player*/) == null &&
			Physics2D.OverlapBox(body.position + otherCenter, wallCollider.size, 0, ~(1 << 8) /*player*/) == null) {
			isWall = false;
		}

		var bashTar = Physics2D.OverlapCapsule(body.position + bashCollider.offset, bashCollider.size, bashCollider.direction, 0, ~(1 << 8) /*player*/);
		pbash.bashTarget = (bashTar && bashTar.tag == "Projectile") ? bashTar.transform : null;
	}

	void OnCollisionStay2D(Collision2D col) {
		var cols = col.contacts;

		foreach (var c in cols) {
			var ang = Vector2.Angle(Vector2.up, c.normal);

			// DETECT GROUNDED
			if (ang <= groundAngleMax) {
				groundNormal = Vector2.SignedAngle(Vector2.up, c.normal);
				isGrounded = true;

				pjump.jumpCount = 0;
				pdash.canDash = true;
			}

			// DETECT WALL
			if (ang > groundAngleMax && ang < wallAngleMax) {
				wallNormal = Vector2.SignedAngle(Vector2.up, c.normal);
				isWall = true;

				pjump.jumpCount = 0;
				pdash.canDash = true;
			}

		}
	}

	#endregion

	void UpdateAnimator() {
		if (horz != 0) {
			lookingRight = horz > 0;
			sr.flipX = !lookingRight;
		}
		sr.transform.localRotation = Quaternion.Euler(0, 0, groundNormal);

	}

	void OverrideNonZero(ref Vector2 vel, Vector2 add) {
		if (add.x != 0) {
			vel.x = add.x;
		}
		if (add.y != 0) {
			vel.y = add.y;
		}
	}

	void OverrideNonInf(ref Vector2 vel, Vector2 add) {
		if (add.x != Mathf.Infinity) {
			vel.x = add.x;
		}
		if (add.y != Mathf.Infinity) {
			vel.y = add.y;
		}
	}

	[System.Serializable]
	public class PlayerStandardMovement : IPlayerSubmovement {
		public Vector2 vel;

		public float groundAccel = 80f;
		[Range(0, 1)] public float groundDeccelMult = 0.85f;
		public float groundSpeed = 12f;
		public int preventDir = 0;

		PlayerMovement super;

		public void Init(PlayerMovement player) {
			super = player;
		}

		public Vector2 Update() {
			var horz = super.horz;

			// SNAP DIRECTION
			if (super.body.velocity.x * horz < 0) {
				if (preventDir * super.body.velocity.x <= 0) { // run if not same sign
					vel.x = 0;
				}
			}

			var preventDirMult = preventDir * horz <= 0 ? 1 : 0.3f;

			// CORE MOVEMENT
			if (horz != 0) {
				if (Mathf.Abs(vel.x) < groundSpeed) {
					vel.x += horz * groundAccel * preventDirMult * Time.fixedDeltaTime;
				}
				vel.x = Mathf.Clamp(vel.x, -groundSpeed * preventDirMult, groundSpeed * preventDirMult);

			} else { // Deccel
				if (Mathf.Abs(vel.x) > 0.05f) {
					vel.x *= groundDeccelMult;
				} else {
					vel.x = 0;
				}
			}

			return Quaternion.Euler(0, 0, super.groundNormal) * vel; // rotate to world
		}

	}

	[System.Serializable]
	public class PlayerStandardDash : IPlayerSubmovement {
		public bool dashing;
		public bool canDash = true;
		public float dashSpeed = 20f;
		public float dashTimeMax = 1f;
		private float dashTime;
		bool dashPostFix;

		private PlayerMovement super;

		public void Init(PlayerMovement player) {
			super = player;
		}

		public Vector2 Update() {
			// INIT
			if (!dashing && InputWrapper.GetDash() && canDash) {
				dashTime = dashTimeMax;
				dashing = true;
				canDash = false;
				dashPostFix = false;
			}

			// DASH
			if (dashing) {
				super.overrideVel = new Vector2(dashSpeed * (super.lookingRight ? 1 : -1), 0);

				if (dashTime > 0 && !super.isWall) {
					dashTime -= Time.fixedDeltaTime;
				} else {
					dashing = false;
				}
			}

			if (!dashPostFix && !dashing) {
				dashPostFix = true;
				super.overrideVel.x = 0;
			}

			return Vector2.zero;
		}
	}

	[System.Serializable]
	public class PlayerJump : IPlayerSubmovement {
		public Vector2 vel;

		public bool jumping;
		public int jumpsMax = 2;
		public int jumpCount;
		public float[] jumpSpeed;
		private Vector2 jumpSpeedCur;
		public float[] jumpTimeMax;
		public float jumpTime;

		PlayerMovement super;

		public void Init(PlayerMovement player) {
			super = player;
		}

		public Vector2 Update() {
			if (jumpCount < jumpsMax && !jumping) { // init
				if ((super.isGrounded && InputWrapper.GetJumpBuffered() && jumpCount == 0) ||
					InputWrapper.GetJumpDown()) { // first jump is buffered

					jumping = true;
					super.partsys.Play();
					jumpTime = jumpTimeMax[jumpCount];
					jumpSpeedCur = new Vector2(0, jumpSpeed[jumpCount]);
				}
			}

			if (jumping) {
				super.isGrounded = false;

				if (jumpTime > 0 && InputWrapper.GetJump()) {
					super.overrideVel.y = jumpSpeedCur.y;

					vel.x = jumpSpeedCur.x;

					jumpTime -= Time.fixedDeltaTime;

				} else { // end jump
					jumpTime = 0;
					jumping = false;
					jumpCount++;
					vel = Vector2.zero;
				}
			}

			return vel;
		}

		public void SetJumpSpeedCur(Vector2 v) {
			jumpSpeedCur = v;
		}

	}

	[System.Serializable]
	public class PlayerWall : IPlayerSubmovement {
		public Vector2 vel;

		public float wallSpeed = 5f;
		public float wallJumpSpeed = 6f;
		public float wallJumpAngle = 30f;
		public float wallJumpTime = 0.07f;
		public float wallJumpMoveSuppress = 0.03f;

		PlayerMovement super;

		public void Init(PlayerMovement player) {
			super = player;
		}

		public Vector2 Update() {

			//if (InputWrapper.GetJumpDown()) {
			//	var jumpV = new Vector2(0, wallJumpSpeed);
			//	jumpV = Quaternion.Euler(0, 0, wallJumpAngle * (super.wallNormal < 0 ? -1 : 1)) * jumpV;
			//	vel = jumpV;
			//	super.pjump.SetJumpSpeedCur(jumpV);

			//	super.isWall = false;
			//	super.pjump.jumping = true;
			//	super.partsys.Play();

			//	super.pjump.jumpTime = wallJumpTime;
			//	super.pjump.jumpCount = 1;

			//	//preventDir = wallNormal < 0 ? -1 : 1;
			//}

			if (InputWrapper.GetWallGrab()) {
				super.body.velocity = Vector2.zero;

				super.applyGravity = false;
				super.trail.enabled = false;

				if (super.vert != 0) {
					var wallV = new Vector2(super.vert * wallSpeed, 0);
					vel = Quaternion.Euler(0, 0, super.wallNormal) * wallV; // rotate to world
					if (vel.x == 0) {
						vel.x = Mathf.Infinity;
					}
				} else {
					vel = Vector2.zero;
				}

			}

			return vel;
		}

	}

	[System.Serializable]
	public class PlayerBash : IPlayerSubmovement {
		public Vector2 vel;

		public Transform bashTarget, bashArrow;
		public float bashAngle;
		private bool bashInit;
		private float bashTimeStarted;
		public float bashTimeMax = 2f;
		public float bashTimeSetupMax = 0.3f;
		public float bashSetupRate = 8000f;
		public float bashSpeed = 8f;
		public float bashKnockback = 8f;
		public float bashCooldownMax = 0.3f;
		private float bashCooldown;

		PlayerMovement super;

		public void Init(PlayerMovement player) {
			super = player;
		}

		public Vector2 Update() {
			UpdateBash();
			return vel;
		}

		public void UpdateBashCommon() {
			if (bashCooldown > 0) {
				bashCooldown -= Time.fixedDeltaTime;
			}

			if (bashTarget) {
				bashArrow.position = bashTarget.position;
				bashArrow.localRotation = Quaternion.Euler(0, 0, bashAngle);

				if (InputWrapper.GetBash() && !bashInit && bashCooldown <= 0) {
					bashInit = true;
					super.mode = MoveMode.BASH;
					bashTimeStarted = Time.unscaledTime;
				}
			}

			var isBash = super.mode == MoveMode.BASH;

			if (!isBash) {
				bashInit = false;
			}

			bashArrow.gameObject.SetActive(isBash);

			Time.timeScale = isBash ? 0.01f : 1;
			Time.fixedDeltaTime = Time.timeScale * super.initFixedDeltaTimescale;
		}

		void UpdateBash() {
			var input = new Vector2(super.horz, super.vert);

			if (input.sqrMagnitude != 0) {
				var mouseAng = Vector2.SignedAngle(Vector2.up, input);

				if (Time.unscaledTime - bashTimeStarted < bashTimeSetupMax) { // initial angle
					bashAngle = mouseAng;

				} else {
					bashAngle = Mathf.MoveTowardsAngle(bashAngle, mouseAng, bashSetupRate * Time.fixedDeltaTime);
				}
			}

			if (!InputWrapper.GetBash() || Time.unscaledTime - bashTimeStarted > bashTimeMax) {
				DoBash();
				super.mode = MoveMode.STANDARD;
			}
		}

		void DoBash() {
			var bashQuat = Quaternion.Euler(0, 0, bashAngle);
			super.body.velocity = bashQuat * new Vector2(0, bashSpeed);
			bashCooldown = bashCooldownMax;

			var tarBody = bashTarget.GetComponent<Rigidbody2D>();
			if (tarBody != null) {
				var oppDir = bashQuat * new Vector2(0, -bashKnockback);
				tarBody.velocity = oppDir;
			}
		}
	}

}
