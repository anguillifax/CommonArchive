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
	public float groundNormal;
	public float frictionSpeedSqr = 0.4f;

	public bool isWall;
	public float wallAngleMax = 120f;
	public float wallNormal;

	public PlayerStandardMovement pmove;
	public PlayerJump pjump;
	public PlayerDash pdash;
	public PlayerWallClimb pwall;
	public PlayerWallJump pwalljump;
	public PlayerBash pbash;

	private bool lookingRight;
	private float horz, vert;
	private float initGravityScale, initFixedDeltaTimescale;

	public BoxCollider2D groundCollider, wallCollider;

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
		pwalljump.Init(this);
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

		applyGravity = true;
		trail.enabled = true;

		UpdateContacts();
		pbash.UpdateBashCommon();

		if (!isGrounded) {
			groundNormal = 0;
		}
		if (!isWall) {
			wallNormal = 0;
		}

		switch (mode) {
			case MoveMode.STANDARD:

				var pmoveVel = pmove.Update();
				OverrideNonZero(ref vel, pmoveVel);

				if (isGrounded && horz == 0) {
					vel.x = pmoveVel.x;
				}

				vel.x += pjump.Update().x;
				pdash.Update();


				if (!isGrounded && isWall) {
					mode = MoveMode.WALL;
				}

				break;
			case MoveMode.WALL:

				trail.enabled = false;

				OverrideNonZero(ref vel, pmove.Update());
				OverrideNonZero(ref vel, pwall.Update());
				OverrideNonZero(ref vel, pwalljump.Update());
				pdash.Update();

				if (isGrounded || !isWall) {
					mode = MoveMode.STANDARD;
				}

				break;
			case MoveMode.BASH:
				if (!pbash.bashTarget) {
					mode = MoveMode.STANDARD;
					break;
				}
				OverrideNonZero(ref vel, pbash.Update());

				break;
			case MoveMode.STOMP:

				break;
			case MoveMode.PUSH:

				break;
			default:
				mode = MoveMode.STANDARD;
				break;
		}

		body.gravityScale = applyGravity ? initGravityScale : 0;

		OverrideNonInf(ref vel, overrideVel);
		body.velocity = vel;
		//pmove.SetX(vel.x);
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
			if (vel.x * horz < 0) {
				if (preventDir * vel.x <= 0) { // run if not same sign
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

		public void Reset() {
			vel.x = 0;
			preventDir = 0;
		}

		public void SetX(float x) {
			vel.x = x;
		}

		public void ZeroDirection(bool right) {
			if ((right && vel.x > 0) || (!right && vel.x < 0)) {
				vel.x = 0;
			}
		}

	}

	[System.Serializable]
	public class PlayerDash : IPlayerSubmovement {
		public Vector2 realVel;

		public bool dashing;
		public bool canDash = true;
		public bool dashRight;
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

				if (super.isWall) { // FIX
					dashRight = super.wallNormal < 0;
				} else {
					dashRight = super.lookingRight;
				}
			}

			// DASH
			if (dashing) {

				realVel = new Vector2(dashSpeed * (dashRight ? 1 : -1), 0);
				super.overrideVel = realVel;

				if (dashTime > 0.03f && super.isWall) {
					dashing = false;
				}

				if (dashTime > 0) {
					dashTime -= Time.fixedDeltaTime;
				} else {
					dashing = false;
				}
			}

			// RESET ONCE
			if (!dashPostFix && !dashing) {
				dashPostFix = true;
				super.overrideVel.x = 0;
				//super.pmove.ZeroDirection(!dashRight);
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
	public class PlayerWallClimb : IPlayerSubmovement {
		public Vector2 vel;

		public float wallSpeed = 5f;

		PlayerMovement super;

		public void Init(PlayerMovement player) {
			super = player;
		}

		public Vector2 Update() {

			if (InputWrapper.GetWallGrab() && super.wallNormal != 0) {
				if (super.vert != 0) {
					var wallV = new Vector2(super.vert * wallSpeed * Mathf.Sign(super.wallNormal), 0);
					vel = Quaternion.Euler(0, 0, super.wallNormal) * wallV; // rotate to world
				} else {
					super.overrideVel = Vector2.zero;
					super.applyGravity = false;
					vel = Vector2.zero;
				}
			} else {
				vel = Vector2.zero;
			}

			return vel;
		}


	}

	[System.Serializable]
	public class PlayerWallJump : IPlayerSubmovement {
		public Vector2 vel;

		public float wallJumpSpeed = 6f;
		public float wallJumpAngle = 30f;
		public float wallJumpTime = 0.07f;

		PlayerMovement super;

		public void Init(PlayerMovement player) {
			super = player;
		}

		public Vector2 Update() {
			if (InputWrapper.GetJumpDown()) {
				var jumpV = Quaternion.Euler(0, 0, wallJumpAngle * Mathf.Sign(super.wallNormal)) * new Vector2(0, wallJumpSpeed);
				vel = jumpV;

				super.pmove.Reset();

				super.partsys.Play();

				super.pjump.SetJumpSpeedCur(jumpV);
				super.pjump.jumping = true;
				super.pjump.jumpTime = wallJumpTime;
				super.pjump.jumpCount = 1;

				//preventDir = wallNormal < 0 ? -1 : 1;
			} else {
				vel = Vector2.zero;
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
		public float bashSpeedFalloffMult = 0.8f;
		public float bashKnockback = 8f;

		public float bashCooldownMax = 0.3f;
		private float bashCooldown;

		public CapsuleCollider2D bashCollider;

		PlayerMovement super;

		public void Init(PlayerMovement player) {
			super = player;
		}

		public Vector2 Update() {
			if (!bashTarget || super.mode != MoveMode.BASH) {
				return Vector2.zero;
			}

			// SET DIRECTION
			var input = new Vector2(super.horz, super.vert);
			if (input.sqrMagnitude != 0) {
				var mouseAng = Vector2.SignedAngle(Vector2.up, input);

				if (Time.unscaledTime - bashTimeStarted < bashTimeSetupMax) { // initial angle
					bashAngle = mouseAng;

				} else {
					bashAngle = Mathf.MoveTowardsAngle(bashAngle, mouseAng, bashSetupRate * Time.fixedDeltaTime);
				}
			}

			// PERFORM BASH
			if (!InputWrapper.GetBash() || Time.unscaledTime - bashTimeStarted > bashTimeMax) {
				DoBash();
				super.mode = MoveMode.STANDARD;
			} else {
				vel = Vector2.zero;
			}

			return vel;
		}

		void DoBash() {
			var bashQuat = Quaternion.Euler(0, 0, bashAngle);
			vel = bashQuat * new Vector2(0, bashSpeed);

			bashCooldown = bashCooldownMax;

			var tarBody = bashTarget.GetComponent<Rigidbody2D>();
			if (tarBody != null) {
				var oppDir = bashQuat * new Vector2(0, -bashKnockback);
				tarBody.velocity = oppDir;
			}
		}

		public void UpdateBashCommon() {
			// RAMP VELOCITY TO ZERO
			//if (vel.sqrMagnitude > 0.5f) {
			//	vel *= bashSpeedFalloffMult;
			//} else {
			//	vel = Vector2.zero;
			//}

			// FIND TARGET
			var bashTar = Physics2D.OverlapCapsule(super.body.position + bashCollider.offset, bashCollider.size, bashCollider.direction, 0, ~(1 << 8) /*player*/);
			bashTarget = (bashTar && bashTar.tag == "Projectile") ? bashTar.transform : null;

			// COOLDOWN
			if (bashCooldown > 0) {
				bashCooldown -= Time.fixedDeltaTime;
			}

			if (bashTarget) {
				// MOVE ARROW
				bashArrow.position = bashTarget.position;
				bashArrow.localRotation = Quaternion.Euler(0, 0, bashAngle);

				// START BASH
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

			// SET BULLET TIME
			Time.timeScale = isBash ? 0.01f : 1;
			Time.fixedDeltaTime = Time.timeScale * super.initFixedDeltaTimescale;
		}
	}

}
