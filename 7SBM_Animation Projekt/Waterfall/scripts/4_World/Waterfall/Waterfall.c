class RaG_WaterFall_Base extends BuildingSuper
{
	protected ref EffWaterFall1 							m_EffWaterFall1;
	protected ref EffWaterFall2 							m_EffWaterFall2;
	protected ref EffWaterFall3 							m_EffWaterFall3;
	protected ref EffWaterFall4 							m_EffWaterFall4;
	protected ref EffWaterFall5 							m_EffWaterFall5;
	protected ref EffWaterFallLarge 						m_EffWaterFallLarge;
	int 	m_WaterFall1Index;
	int 	m_WaterFall2Index;
	int 	m_WaterFall3Index;
	int 	m_WaterFall4Index;
	int 	m_WaterFall5Index;
	int 	m_WaterFallLargeIndex;
	
	protected ref EffectSound					m_WaterfallSound;
	
	override void EEInit()
	{
		super.EEInit();
		
		if ( !GetGame().IsDedicatedServer() )
		{
			SpawnWaterFallParticle();
			PlayWaterFallSound();
		}
	}
	
	void PlayWaterFallSound()
	{
		if ( !m_WaterfallSound )
		{
			vector sound_pos = ModelToWorld( GetMemoryPointPos( "waterfall" ) );
			m_WaterfallSound = SEffectManager.PlaySound( "rag_waterfall_SoundSet" , sound_pos, 0, 0, true );
			m_WaterfallSound.SetSoundMaxVolume( 2 );
			m_WaterfallSound.SetParent( this );
			m_WaterfallSound.SetSoundAutodestroy( false );
		}
	}
	
	void SpawnWaterFallParticle()
	{
		if (!m_EffWaterFall1)
		{
			m_EffWaterFall1 = new EffWaterFall1();
				
			if ( m_EffWaterFall1 && !SEffectManager.IsEffectExist(m_WaterFall1Index) )
			{
				m_WaterFall1Index = SEffectManager.PlayOnObject(m_EffWaterFall1, this, "0 1 0");
				m_EffWaterFall1.GetParticle();
			}
		}
	}
	
	override void EEDelete(EntityAI parent)
	{
		if ( !GetGame().IsDedicatedServer() )
		{
			if ( m_EffWaterFall1 )
			{
				SEffectManager.DestroyEffect( m_EffWaterFall1 );
			}
			
			if ( m_EffWaterFall2 )
			{
				SEffectManager.DestroyEffect( m_EffWaterFall2 );
			}
			
			if ( m_EffWaterFall3 )
			{
				SEffectManager.DestroyEffect( m_EffWaterFall3 );
			}
			
			if ( m_EffWaterFall4 )
			{
				SEffectManager.DestroyEffect( m_EffWaterFall4 );
			}
			
			if ( m_EffWaterFall5 )
			{
				SEffectManager.DestroyEffect( m_EffWaterFall5 );
			}
			
			if ( m_EffWaterFallLarge )
			{
				SEffectManager.DestroyEffect( m_EffWaterFallLarge );
			}
			
			if ( m_WaterfallSound )
			{
				StopSoundSet(m_WaterfallSound);
			}
		}
	}
};

class RaG_WaterFall : RaG_WaterFall_Base{};
class RaG_WaterFall1 : RaG_WaterFall_Base{};

class RaG_WaterFall2 : RaG_WaterFall_Base
{
	override void SpawnWaterFallParticle()
	{
		if (!m_EffWaterFall2)
		{
			m_EffWaterFall2 = new EffWaterFall2();
				
			if ( m_EffWaterFall2 && !SEffectManager.IsEffectExist(m_WaterFall2Index) )
			{
				m_WaterFall2Index = SEffectManager.PlayOnObject(m_EffWaterFall2, this, "0 1 0");
				m_EffWaterFall2.GetParticle();
			}
		}
	}
};

class RaG_WaterFall3 : RaG_WaterFall_Base
{
	override void SpawnWaterFallParticle()
	{
		if (!m_EffWaterFall3)
		{
			m_EffWaterFall3 = new EffWaterFall3();
				
			if ( m_EffWaterFall3 && !SEffectManager.IsEffectExist(m_WaterFall3Index) )
			{
				m_WaterFall3Index = SEffectManager.PlayOnObject(m_EffWaterFall3, this, "0 1 0");
				m_EffWaterFall3.GetParticle();
			}
		}
	}
};

class RaG_WaterFall4 : RaG_WaterFall_Base
{
	override void SpawnWaterFallParticle()
	{
		if (!m_EffWaterFall4)
		{
			m_EffWaterFall4 = new EffWaterFall4();
				
			if ( m_EffWaterFall4 && !SEffectManager.IsEffectExist(m_WaterFall4Index) )
			{
				m_WaterFall4Index = SEffectManager.PlayOnObject(m_EffWaterFall4, this, "0 1 0");
				m_EffWaterFall4.GetParticle();
			}
		}
	}
};

class RaG_WaterFall5 : RaG_WaterFall_Base
{
	override void SpawnWaterFallParticle()
	{
		if (!m_EffWaterFall5)
		{
			m_EffWaterFall5 = new EffWaterFall5();
				
			if ( m_EffWaterFall5 && !SEffectManager.IsEffectExist(m_WaterFall5Index) )
			{
				m_WaterFall5Index = SEffectManager.PlayOnObject(m_EffWaterFall5, this, "0 1 0");
				m_EffWaterFall5.GetParticle();
			}
		}
	}
};

class RaG_WaterFallLarge : RaG_WaterFall_Base
{
	override void SpawnWaterFallParticle()
	{
		if (!m_EffWaterFallLarge)
		{
			m_EffWaterFallLarge = new EffWaterFallLarge();
				
			if ( m_EffWaterFallLarge && !SEffectManager.IsEffectExist(m_WaterFallLargeIndex) )
			{
				m_WaterFallLargeIndex = SEffectManager.PlayOnObject(m_EffWaterFallLarge, this, "0 1 0");
				m_EffWaterFallLarge.GetParticle();
			}
		}
	}
	
	override void PlayWaterFallSound()
	{
		if ( !m_WaterfallSound )
		{
			vector sound_pos = ModelToWorld( GetMemoryPointPos( "waterfall" ) );
			m_WaterfallSound = SEffectManager.PlaySound( "rag_waterfall_SoundSet" , sound_pos, 0, 0, true );
			m_WaterfallSound.SetSoundMaxVolume( 7 );
			m_WaterfallSound.SetParent( this );
			m_WaterfallSound.SetSoundAutodestroy( false );
		}
	}
};