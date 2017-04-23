/*
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using QuantConnect;
using QuantConnect.Configuration;

namespace Optimization.Example
{
    public class ParameterizedOCA : OCAExample
    {

        public override AlgoSettings InitStrategyParameters()
        {


            var res = new AlgoSettings()
            {
                fastEmaSpeed = Config.GetInt("fastEmaSpeed", 3),
                slowEmaSpeed = Config.GetInt("slowEmaSpeed", 90),

                liquidateOnFridays = (Config.GetInt("liquidateOnFridays", 1)) == 0 ? false : true,

                recentBars = Config.GetInt("recentBars", 23),
                veryRecentBars = Config.GetInt("veryRecentBars", 5),

                recentBuffer = (decimal)Config.GetDouble("recentBuffer", 0.5),
                veryRecentBuffer = (decimal)Config.GetDouble("veryRecentBuffer", 0.16),

                minimumPlayProfit = (decimal)Config.GetDouble("minimumPlayProfit", 0.25)
            };


            if (res.fastEmaSpeed >= res.slowEmaSpeed) throw new System.Exception("Invalid setup");
            if (res.recentBars <= res.veryRecentBars) throw new System.Exception("Invalid setup");
            if (res.recentBuffer <= res.veryRecentBuffer) throw new System.Exception("Invalid setup");

            return res;

        }
    }
}
