using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;

namespace E2A___Ammo_From_Energy.E2A
{
   public class E2A
    {
        public MyDefinitionId ammoID;
        private E2AStateDrill E2AState;
        
        public float m_PowerMulti { get { return E2ASession.Static?.Settings.m_PowerMulti ?? 1.0f; } }

        public enum OperationStateEnum
        {
            Idle = 0, Running, Paused, Stopping
        }

        [ProtoContract]
        public class E2AStateDrill
        {
            [ProtoMember(1)]
            public OperationStateEnum OperationState = OperationStateEnum.Idle;
        }

        private E2AStateDrill m_state;
        public bool IsIdle => State.OperationState == OperationStateEnum.Idle;
        public bool IsPaused => State.OperationState == OperationStateEnum.Paused;
        public bool IsRunning => State.OperationState == OperationStateEnum.Running;
        public bool IsStopping => State.OperationState == OperationStateEnum.Stopping;

        public E2AStateDrill State
        {
            get { return m_state; }
            set
            {
                m_state = value;
                if (m_state.OperationState == OperationStateEnum.Running)
                {
                    //InitMakeAmmo();
                }
            }
        }


        public void Pause()
        {
            State.OperationState = OperationStateEnum.Paused;
        }

        public void Resume()
        {
            if (IsPaused)
                State.OperationState = OperationStateEnum.Running;
        }

        public void Stop()
        {
            State.OperationState = OperationStateEnum.Stopping;
        }
    } 
}
